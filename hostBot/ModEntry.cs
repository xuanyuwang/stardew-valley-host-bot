﻿using System;
using System.Runtime.Intrinsics.X86;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Netcode;
using StardewModdingAPI;
using StardewModdingAPI.Events;
using StardewModdingAPI.Utilities;
using StardewValley;
using StardewValley.Menus;

namespace hostBot
{
    /// <summary>The mod entry point.</summary>
    public class ModEntry : Mod
    {
        private bool _isHostBotMode;
        private bool _isAutoSleep;
        private int _autoSleepTime = 2200;
        private string? _lastChatMessage;

        private string _goodNightMessage = "我先睡了，晚安玛卡巴卡";
        private Dictionary<string, int > _dailyMessages = new Dictionary<string, int >();

        // game loop status
        private bool _dayEnding;

        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.ConsoleCommands.Add("hostbot", "Host bot commands", this.HostBotCommands);

            helper.Events.GameLoop.SaveCreating += (sender, args) => { Monitor.Log("Save Creating", LogLevel.Info); };
            helper.Events.GameLoop.SaveCreated += (sender, args) => { Monitor.Log("Save Created", LogLevel.Info); };
            helper.Events.GameLoop.Saving += (sender, args) => { Monitor.Log("Saving", LogLevel.Info); };
            helper.Events.GameLoop.Saved += (sender, args) => { Monitor.Log("Saved", LogLevel.Info); };
            helper.Events.GameLoop.SaveLoaded += (sender, args) => { Monitor.Log("Save Loaded", LogLevel.Info); };

            // set / unset DayEnding
            helper.Events.GameLoop.DayStarted += (sender, args) =>
            {
                Monitor.Log("Day Started", LogLevel.Info);
                this._dayEnding = false;
                
                this._dailyMessages.Clear();
                this._dailyMessages.Add(_goodNightMessage, 1);
            };
            helper.Events.GameLoop.DayEnding += (sender, args) =>
            {
                Monitor.Log("Day Ending", LogLevel.Info);
                this._dayEnding = true;
            };
            helper.Events.GameLoop.OneSecondUpdateTicking += (sender, args) =>
            {
                CloseShippingMenu();
                CloseAnyBlockingMenus();
                TryRunChatMessage();
            };
            helper.Events.GameLoop.TimeChanged += (sender, args) => { AutoGoToBed(); };

            // Resume the world when there is a remote player
            helper.Events.Multiplayer.PeerConnected += (sender, args) =>
            {
                Monitor.Log("PeerConnected", LogLevel.Info);
                if (Context.HasRemotePlayers)
                {
                    PauseWorld(false);
                    this.HostBotCommands("", new string[] { "on" });
                    this.HostBotCommands("", new string[] { "sleep", _autoSleepTime.ToString() });
                }
            };

            // Pause the world when there is no remote player
            helper.Events.Multiplayer.PeerDisconnected += (sender, args) =>
            {
                Monitor.Log("PeerDisconnected", LogLevel.Info);
                if (!Context.HasRemotePlayers)
                {
                    Monitor.Log("There is no other remote player", LogLevel.Info);
                    PauseWorld(true);
                    this.HostBotCommands("", new string[] { "off" });
                    this.HostBotCommands("", new string[] { "sleep", "no" });
                }
                else
                {
                    Monitor.Log("There are still other remote players", LogLevel.Info);
                }
            };
        }
        
        private void PauseWorld(bool pause)
        {
            Game1.netWorldState.Value.IsPaused = pause;

            var label = pause ? "paused" : "resumed";
            var message = $"The world is ${label}";
            Monitor.Log(message, LogLevel.Info);
            MultiplayerChatMessage(message);
        }

        private void AutoGoToBed()
        {
            var currentTime = Game1.timeOfDay;
            if (this._isAutoSleep)
            {
                if (currentTime >= _autoSleepTime)
                {
                    GoToBed(new string[] { });
                }
            }
        }

        /// <summary>
        /// Try to close the shipping menu after the DayEnding
        /// </summary>
        private void CloseShippingMenu()
        {
            if (this._isAutoSleep && this._dayEnding)
            {
                if (Game1.activeClickableMenu is ShippingMenu)
                {
                    if (Game1.activeClickableMenu.IsActive() && Game1.activeClickableMenu.readyToClose())
                    {
                        Monitor.Log("The shipping menu is ready to close", LogLevel.Info);
                        var menu = Game1.activeClickableMenu;
                        for (var i = 0; i < menu.allClickableComponents.Count; i++)
                        {
                            var component = menu.allClickableComponents[i];
                            if (component.myID != 101) continue;
                            Monitor.Log("Clicking the OK button", LogLevel.Info);
                            var okButton = component;
                            menu.receiveLeftClick(okButton.bounds.Center.X, okButton.bounds.Center.Y, true);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Try to close any non-shipping-menu after a day ending
        /// </summary>
        private void CloseAnyBlockingMenus()
        {
            if (this._isAutoSleep && this._dayEnding)
            {
                if (Game1.activeClickableMenu != null && Game1.activeClickableMenu.IsActive() &&
                    Game1.activeClickableMenu.readyToClose() &&
                    Game1.activeClickableMenu is not ShippingMenu)
                {
                    Monitor.Log("A menu is blocking after day ending. Closing it", LogLevel.Info);
                    Game1.activeClickableMenu.exitThisMenu();
                }
            }
        }

        private (int, int) GetBedCoordinates()
        {
            int bedX, bedY;
            var houseUpgradeLevel = Game1.player.HouseUpgradeLevel;
            Monitor.Log($"the hose upgrade level is {houseUpgradeLevel}", LogLevel.Info);
            switch (houseUpgradeLevel)
            {
                case 0:
                    bedX = 9;
                    bedY = 9;
                    break;
                case 1:
                    bedX = 21;
                    bedY = 4;
                    break;
                default:
                    bedX = 27;
                    bedY = 13;
                    break;
            }

            return (bedX, bedY);
        }

        private void Sleep()
        {
            var (x, y) = GetBedCoordinates();
            Game1.warpFarmer("Farmhouse", x, y, false);
            this.Helper.Reflection.GetMethod(Game1.currentLocation, "startSleep").Invoke();
            MultiplayerDailyChatMessage(this._goodNightMessage);
        }

        /// <summary>
        /// When no arg is provided, the host will sleep
        /// When "no" is provided, the host will not auto sleep
        /// When "hhmm" is provided, the host will auto sleep after that time
        /// </summary>
        /// <param name="args"></param>
        private void GoToBed(string[] args)
        {
            if (args.Length == 0)
            {
                MultiplayerChatMessage("the host bot will sleep immediately");
                Sleep();
                return;
            }

            var arg = args[0];
            switch (arg)
            {
                case "no":
                    MultiplayerChatMessage("the host bot will NOT auto sleep");
                    _isAutoSleep = false;
                    break;
                default:
                    var validTime = arg.Length == 4 && arg.All(char.IsDigit);
                    if (validTime)
                    {
                        MultiplayerChatMessage($"the host bot will auto sleep at {arg}");
                        _autoSleepTime = int.Parse(arg);
                        _isAutoSleep = true;
                    }

                    break;
            }
        }

        private void HostBotCommands(string command, string[] args)
        {
            Monitor.Log($"command: {command}", LogLevel.Info);

            if (args.Length == 0)
            {
                return;
            }

            for (int i = 0; i < args.Length; i++)
            {
                Monitor.Log($"args {i}: {args[i]}", LogLevel.Info);
            }

            var subcommand = args[0];
            Monitor.Log($"subcommand: {subcommand}", LogLevel.Info);
            var subArgs = args.Skip(1).ToArray();
            for (int i = 0; i < subArgs.Length; i++)
            {
                Monitor.Log($"subArgs {i}: {subArgs[i]}", LogLevel.Info);
            }

            switch (subcommand.ToLower())
            {
                case "sleep":
                    GoToBed(subArgs);
                    break;
                case "on":
                    ToggleHostBotMode(true);
                    break;
                case "off":
                    ToggleHostBotMode(false);
                    break;
            }
        }

        /// <summary>
        /// Set the bot mode on/off. If targetStatus is True, set the bot mode to on. Otherwise, set the bot mode to off.
        /// </summary>
        /// <param name="targetStatus"></param>
        private void ToggleHostBotMode(bool targetStatus)
        {
            if (!Context.IsWorldReady) return;
            if (_isHostBotMode == targetStatus) return;

            _isHostBotMode = targetStatus;
            var status = targetStatus ? "on" : "off";
            var message = $"The host bot mode is now {status}.";
            this.Monitor.Log(message, LogLevel.Info);
            Game1.chatBox.globalInfoMessage(message);
        }

        private void TryRunChatMessage()
        {
            if (!Context.IsWorldReady) return;
            if (Game1.chatBox.messages.Count == 0) return;

            var lastMessage = Game1.chatBox.messages.Last();
            var plainText = ChatMessage.makeMessagePlaintext(lastMessage.message, false);
            if (plainText == _lastChatMessage) return;

            _lastChatMessage = plainText;
            var (command, args) = ParseChatMessage(plainText);
            if (command != null && args != null)
            {
                HostBotCommands(command, args);
            }
        }

        private (string?, string[]?) ParseChatMessage(string chatMessage)
        {
            Monitor.Log(chatMessage, LogLevel.Info);
            var trimmedMessage = chatMessage.Trim();
            if (trimmedMessage.Length == 0) return (null, null);
            Monitor.Log($"trimmed message {chatMessage}", LogLevel.Info);

            var splitMessages = trimmedMessage.Split("!!");
            if (splitMessages.Length == 1) return (null, null);

            var command = splitMessages[1];
            Monitor.Log($"command from message {command}", LogLevel.Info);
            var commandParts = command.Split(' ');
            return (commandParts[0], commandParts.Skip(1).ToArray());
        }

        private void MultiplayerDailyChatMessage(string message)
        {
            if(!this._dailyMessages.ContainsKey(message)) return;
            
            var counter = _dailyMessages[message];
            if(counter < 1) return;
            _dailyMessages[message] = counter - 1;
            MultiplayerChatMessage(message);
        }

        private void MultiplayerChatMessage(string message)
        {
            Game1.chatBox.globalInfoMessage(message);
        }
    }
}