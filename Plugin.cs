using BepInEx;
using HarmonyLib;
using BepInEx.Logging;
using System;
using Buttplug.Client;
using Buttplug.Client.Connectors.WebsocketConnector;
using System.Timers;
using System.Threading.Tasks;
using UnityEngine;
using Battle;

namespace PeglinCore
{
    [BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
    [BepInProcess("Peglin.exe")]
    [HarmonyPatch]
    public class Plugin : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony(PluginInfo.PLUGIN_GUID);
        internal static ManualLogSource Log;
        internal static ButtplugClient client;
        internal static Timer timer;
        internal static bool quitting = false;

        private void Awake()
        {
            harmony.PatchAll();
            Log = base.Logger;
            Log.LogInfo($"Plugin {PluginInfo.PLUGIN_GUID} is loaded!");
            client = new ButtplugClient("Pegginglin");
            Peg.OnPegHit += HandlePegHit;       // Adds event handler for peg hits
            
            Task.Run(async () => {
                while (true)
                {
                    Log.LogInfo("Starting Buttplug Client");
                    if (quitting)
                    {
                        return;
                    }
                    if (!client.Connected)
                    {
                        try
                        {
                            await client.ConnectAsync(new ButtplugWebsocketConnector(new Uri("ws://127.0.0.1:12345")));
                            Log.LogInfo("Buttplug Client Started");
                        }
                        catch (Exception ex)
                        {
                            Log.LogError(ex);
                        }
                    }

                    // Wait for 5 seconds then try connecting again.
                    await Task.Delay(5000);
                }
            });

            Application.quitting += OnApplicationQuit;
            
            timer = new Timer();
            timer.Elapsed += OnTimerElapsed;
        }

        static void OnApplicationQuit()
        {
            Log.LogInfo("Application quitting, spinning down buttplug client");
            quitting = true;
            if (client != null)
            {
                Task.Run(async () =>
                {
                    if (client.Connected)
                    {
                        await client.DisconnectAsync();
                    }
                    client = null;
                });
            }
        }

        static void OnTimerElapsed(object o, EventArgs e)
        {
            client.StopAllDevicesAsync();
            timer.Stop();
        }

        static void RunVibrate(double value)
        {
            Log.LogDebug("RunVibrate called with value: " + value);
            if (!timer.Enabled)
            {
                if (client.Devices != null)
                {
                    foreach (var d in client.Devices)
                    {
                        if (d.VibrateAttributes.Count > 0)
                        {
                            Log.LogDebug("Successfully reached actual Vibrate trigger");
                            d.VibrateAsync(value);
                        }
                    }
                }
                timer.Start();
            }
            timer.Stop();
            timer.Start();
        }

        static private void EvaluatePeg(Peg peg, Peg.PegType pegType)
        {
            switch (pegType)
            {
                case Peg.PegType.REGULAR:   // Regular peg
                    RunVibrate(0.4);
                    break;
                case Peg.PegType.CRIT:      // Yellow crit peg
                    Log.LogDebug("CRIT");
                    RunVibrate(0.8);
                    break;
                case Peg.PegType.DULL:      // I think these are the gray pegs that don't pop on hit, from certain enemies
                    Log.LogDebug("DULL");
                    RunVibrate(0.3);
                    break;
                case Peg.PegType.BOUNCER:   // I think these are the permanent bouncy pegs that never pop
                    Log.LogDebug("BOUNCER");
                    RunVibrate(0.4);
                    break;
                case Peg.PegType.GOLD:      // I think this only applies to the special gold pegs, not the regular ones
                    Log.LogDebug("GOLD");
                    RunVibrate(0.7);
                    break;
                case Peg.PegType.BOMB:
                    if (peg.GetType() == typeof(Bomb))
                    {
                        Bomb bomb = (Bomb)peg;
                        if (bomb.detonatedThisTurn)     // Pretty sure this doesn't actually work and always goes to the "first hit" branch
                        {
                            Log.LogDebug("Bomb EXPLODE");
                            RunVibrate(1.0);
                        }
                        else
                        {
                            Log.LogDebug("Bomb First Hit");
                            RunVibrate(0.9);
                        }
                    }
                    break;
                case Peg.PegType.RESET:     // The green R pegs for resetting the board
                    Log.LogDebug("RESET");
                    RunVibrate(0.6);
                    break;
                default:
                    Log.LogDebug("OTHER PEG TYPE: " + peg.pegType.ToString());
                    RunVibrate(0.4);
                    break;
            }
        }

        private static void HandlePegHit(Peg.PegType pegType, Peg peg)
        {
            Log.LogDebug("Event logged PegType " + pegType);
            EvaluatePeg(peg, pegType);
        }

    }
}
