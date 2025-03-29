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

        // game loop status
        private bool DayEnding;

        /*********
         ** Public methods
         *********/
        /// <summary>The mod entry point, called after the mod is first loaded.</summary>
        /// <param name="helper">Provides simplified APIs for writing mods.</param>
        public override void Entry(IModHelper helper)
        {
            helper.ConsoleCommands.Add("hostbot", "Toggles bot mode of the host", this.ToggleHostBotMode);
            helper.ConsoleCommands.Add("host_goto_bed", "The hostbot will go to bed", this.GoToBed);

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
            helper.Events.GameLoop.OneSecondUpdateTicking += (sender, args) => { CloseShippingMenu(); };

            // Resume the world when there is a remote player
            helper.Events.Multiplayer.PeerConnected += (sender, args) =>
            {
                Monitor.Log("PeerConnected", LogLevel.Info);
                if (this.IsHostBotMode && Context.HasRemotePlayers)
                {
                    Game1.netWorldState.Value.IsPaused = false;
                }
            };

            // Pause the world when there is no remote player
            helper.Events.Multiplayer.PeerDisconnected += (sender, args) =>
            {
                Monitor.Log("PeerDisconnected", LogLevel.Info);
                if (this.IsHostBotMode && !Context.HasRemotePlayers)
                {
                    Game1.netWorldState.Value.IsPaused = false;
                }
            };
        }


        /// <summary>
        /// Try to close the shipping menu after the DayEnding
        /// </summary>
        private void CloseShippingMenu()
        {
            if (this.IsHostBotMode && this.DayEnding)
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

        private (int, int) GetBetCoordinates()
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

        private void GoToBed(string command, string[] args)
        {
            Game1.chatBox.addInfoMessage("The host bot is going to bed.");
            var (x, y) = GetBetCoordinates();
            Game1.warpFarmer("Farmhouse", x, y, false);
            this.Helper.Reflection.GetMethod(Game1.currentLocation, "startSleep").Invoke();
            Game1.chatBox.addInfoMessage("The host bot mode is now sleeping.");
        }

        // toggles the bot mode on/off
        private void ToggleHostBotMode(string command, string[] args)
        {
            if (Context.IsWorldReady)
            {
                if (!IsHostBotMode)
                {
                    IsHostBotMode = true;
                    this.Monitor.Log("Host bot mode is now enabled.", LogLevel.Info);
                    Game1.chatBox.addInfoMessage("The host bot mode is now enabled.");

                    Game1.displayHUD = true;
                    Game1.addHUDMessage(new HUDMessage("Host bot mode is on"));

                    Game1.options.pauseWhenOutOfFocus = false;
                }
                else
                {
                    IsHostBotMode = false;
                    this.Monitor.Log("Host bot mode is now disabled.", LogLevel.Info);
                    Game1.chatBox.addInfoMessage("The host bot mode is now disabled.");

                    Game1.displayHUD = false;
                    Game1.addHUDMessage(new HUDMessage("Host bot mode is off"));

                    Game1.options.pauseWhenOutOfFocus = true;
                }
            }
        }
    }
}