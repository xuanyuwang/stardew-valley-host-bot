using System;
using System.Runtime.Intrinsics.X86;
using Microsoft.Xna.Framework;
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
        private bool IsHostBotMode;
        private bool IsAutoSleep;
        private int AutoSleepTime = 2200;

        // game loop status
        private bool DayEnding;

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
                this.DayEnding = false;
            };
            helper.Events.GameLoop.DayEnding += (sender, args) =>
            {
                Monitor.Log("Day Ending", LogLevel.Info);
                this.DayEnding = true;
            };
            helper.Events.GameLoop.OneSecondUpdateTicking += (sender, args) =>
            {
                CloseShippingMenu();
                CloseAnyBlockingMenus();
            };
            helper.Events.GameLoop.TimeChanged += (sender, args) => { AutoGoToBed(); };

            // Resume the world when there is a remote player
            helper.Events.Multiplayer.PeerConnected += (sender, args) =>
            {
                Monitor.Log("PeerConnected", LogLevel.Info);
                if (Context.HasRemotePlayers)
                {
                    PauseWorld(false);
                    this.HostBotCommands("", new string[]{"on"});
                    this.HostBotCommands("", new string[]{"sleep", AutoSleepTime.ToString()});
                }
            };

            // Pause the world when there is no remote player
            helper.Events.Multiplayer.PeerDisconnected += (sender, args) =>
            {
                Monitor.Log("PeerDisconnected", LogLevel.Info);
                if (!Context.HasRemotePlayers)
                {
                    PauseWorld(true);
                    this.HostBotCommands("", new string[]{"off"});
                    this.HostBotCommands("", new string[]{"sleep", "no"});
                }
            };
        }

        private void PauseWorld(bool pause)
        {
            Game1.netWorldState.Value.IsPaused = pause;
            
            var label = pause ? "paused" : "resumed";
            var message = $"The world is ${label}";
            Monitor.Log(message, LogLevel.Info);
            Game1.chatBox.addMessage(message, Color.Yellow);
        }

        private void AutoGoToBed()
        {
            var currentTime = Game1.timeOfDay;
            if (this.IsAutoSleep)
            {
                if (currentTime >= AutoSleepTime)
                {
                    GoToBed(new string[]{});
                }
            }
        }

        /// <summary>
        /// Try to close the shipping menu after the DayEnding
        /// </summary>
        private void CloseShippingMenu()
        {
            if (this.IsAutoSleep && this.DayEnding)
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
            if (this.IsAutoSleep && this.DayEnding)
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
            Game1.chatBox.addMessage("我先睡了，晚安玛卡巴卡", Color.Yellow);
        }

        /// <summary>
        /// When no arg is provided, the host will sleep
        /// When "no" is provided, the host will not auto sleep
        /// When "hhmm" is provided, the host will auto sleep after that time
        /// </summary>
        /// <param name="args"></param>
        private void GoToBed(string[] args)
        {
            var arg = args[0];
            switch (arg)
            {
                case null:
                    Sleep();
                    break;
                case "no":
                    IsAutoSleep = false;
                    break;
                default:
                    var validTime = arg.Length == 4 && arg.All(char.IsDigit);
                    if (validTime)
                    {
                        AutoSleepTime = int.Parse(arg);
                    }
                    break;
            }
        }

        private void HostBotCommands(string command, string[] args)
        {
            var subcommand = args[0];
            var subArgs = args.Skip(1).ToArray();
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
            if (IsHostBotMode == targetStatus) return;
            
            IsHostBotMode = targetStatus;
            var status = targetStatus ? "on" : "off";
            var message = $"The host bot mode is now {status}.";
            this.Monitor.Log(message, LogLevel.Info);
            Game1.chatBox.addMessage(message, Color.Yellow);
        }

        private void parseChatMessage(string command, string[] args)
        {
        }
    }
}