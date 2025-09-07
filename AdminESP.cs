using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Core.Attributes.Registration;
using CounterStrikeSharp.API.Modules.Admin;
using CounterStrikeSharp.API.Modules.Commands;
using CounterStrikeSharp.API.Modules.Cvars;
using CounterStrikeSharp.API.Modules.Utils;


namespace AdminESP;

public sealed partial class AdminESP : BasePlugin, IPluginConfig<Config>
{
    public override string ModuleName => "Admin ESP";
    public override string ModuleAuthor => "AquaVadis & GSM-RO";
    public override string ModuleVersion => "1.2.0s";
    public override string ModuleDescription => "Admin ESP plugin adapted for CSS v335 with bot control fix";

    private bool[] toggleAdminESP = new bool[64];
    public bool togglePlayersGlowing = false;
    public Config Config { get; set; } = new();
    private static readonly ConVar? _forceCamera = ConVar.Find("mp_forcecamera");

    public override void Load(bool hotReload)
    {
        RegisterListeners();

        if (hotReload)
        {
            foreach (var player in Utilities.GetPlayers().Where(p => p != null && p.IsValid && p.Connected == PlayerConnectedState.PlayerConnected))
            {
                if (!cachedPlayers.Contains(player))
                    cachedPlayers.Add(player);

                toggleAdminESP[player.Slot] = false;
            }
        }
    }

    public override void Unload(bool hotReload)
    {
        DeregisterListeners();
    }

    [ConsoleCommand("css_esp", "Toggle Admin ESP")]
    [CommandHelper(minArgs: 0, whoCanExecute: CommandUsage.CLIENT_ONLY)]
    public void OnToggleAdminEsp(CCSPlayerController? player, CommandInfo command)
    {
        if (player == null || !player.IsValid) return;

        if (!AdminManager.PlayerHasPermissions(player, Config.AdminFlag))
        {
            SendMessageToSpecificChat(player, "Admin ESP can only be used from {GREEN}admins{DEFAULT}!", PrintTo.Chat);
            return;
        }

        // Dezactivează ESP automat dacă controlezi un bot sau jucător activ
        if (player.PawnIsAlive && player.Team != CsTeam.Spectator)
        {
            toggleAdminESP[player.Slot] = false;
            SendMessageToSpecificChat(player, "You should be {RED}dead {DEFAULT}to use Admin ESP!.", PrintTo.Chat);
            return;
        }

        if (player.Team == CsTeam.Spectator || (Config.AllowDeadAdminESP && !player.PawnIsAlive))
        {
            toggleAdminESP[player.Slot] = !toggleAdminESP[player.Slot];

            if (toggleAdminESP[player.Slot])
            {
                if (!togglePlayersGlowing || !AreThereEsperingAdmins())
                    SetAllPlayersGlowing();
            }
            else
            {
                if (!togglePlayersGlowing || !AreThereEsperingAdmins())
                    RemoveAllGlowingPlayers();
            }

            SendMessageToSpecificChat(player,
                $"Admin ESP has been {(toggleAdminESP[player.Slot] ? "{GREEN}enabled!" : "{RED}disabled!")}",
                PrintTo.Chat);
        }
    }

    public void OnConfigParsed(Config config)
    {
        Config = config;
    }

    // Functie de reset ESP cand slot-ul preia controlul unui pawn activ
    private void ResetESPIfPlayerControlsPawn(CCSPlayerController? player)
    {
        if (player == null || !player.IsValid) return;

        if (player.PawnIsAlive && player.Team != CsTeam.Spectator)
        {
            toggleAdminESP[player.Slot] = false;
            RemoveAllGlowingPlayers();
        }
    }
}
