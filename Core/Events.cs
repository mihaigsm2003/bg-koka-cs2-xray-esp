using CounterStrikeSharp.API;
using CounterStrikeSharp.API.Core;
using CounterStrikeSharp.API.Modules.Entities;
using CounterStrikeSharp.API.Modules.Utils;
using System.Linq;

namespace AdminESP;

public partial class AdminESP
{
    private void RegisterListeners()
    {
        RegisterListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RegisterListener<Listeners.OnClientConnected>(OnClientConnected);
        RegisterListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RegisterListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
        RegisterListener<Listeners.CheckTransmit>(CheckTransmitListener);

        RegisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
        RegisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        RegisterEventHandler<EventRoundStart>(OnRoundStart);
    }

    private void DeregisterListeners()
    {
        RemoveListener<Listeners.OnClientAuthorized>(OnClientAuthorized);
        RemoveListener<Listeners.OnClientConnected>(OnClientConnected);
        RemoveListener<Listeners.OnClientPutInServer>(OnClientPutInServer);
        RemoveListener<Listeners.OnClientDisconnect>(OnClientDisconnected);
        RemoveListener<Listeners.CheckTransmit>(CheckTransmitListener);

        DeregisterEventHandler<EventPlayerSpawn>(OnPlayerSpawn, HookMode.Pre);
        DeregisterEventHandler<EventPlayerDeath>(OnPlayerDeath);
        DeregisterEventHandler<EventRoundStart>(OnRoundStart);
    }

    private void OnClientAuthorized(int slot, SteamID steamid)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !player.IsValid) return;

        if (!cachedPlayers.Contains(player))
            cachedPlayers.Add(player);

        toggleAdminESP[slot] = false;
    }

    private void OnClientConnected(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !player.IsValid) return;

        if (!cachedPlayers.Contains(player))
            cachedPlayers.Add(player);

        toggleAdminESP[slot] = false;
    }

    private void OnClientPutInServer(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null) return;

        if (!cachedPlayers.Contains(player))
            cachedPlayers.Add(player);

        // Reset ESP dacă slot-ul preia controlul unui bot
        ResetESPIfPlayerControlsPawn(player);
    }

    private void CheckTransmitListener(CCheckTransmitInfoList infoList)
    {
        foreach ((CCheckTransmitInfo info, CCSPlayerController? player) in infoList)
        {
            if (player == null || !player.IsValid) continue;

            ResetESPIfPlayerControlsPawn(player);

            for (int i = 0; i < cachedPlayers.Count; i++)
            {
                var cached = cachedPlayers[i];
                if (cached == null || !cached.IsValid) continue;

                if (Config.HideAdminSpectators && cached.Slot != player.Slot)
                {
                    var targetObserverPawn = cached.ObserverPawn.Value;
                    if (targetObserverPawn != null && targetObserverPawn.IsValid)
                        info.TransmitEntities.Remove((int)targetObserverPawn.Index);
                }

                if (toggleAdminESP[player.Slot])
                    continue;

                foreach (var glowingProp in glowingPlayers)
                {
                    if (glowingProp.Value.Item1 != null && glowingProp.Value.Item1.IsValid
                    && glowingProp.Value.Item2 != null && glowingProp.Value.Item2.IsValid)
                    {
                        info.TransmitEntities.Remove((int)glowingProp.Value.Item1.Index);
                        info.TransmitEntities.Remove((int)glowingProp.Value.Item2.Index);
                    }
                }
            }
        }
    }
    public HookResult OnPlayerSpawn(EventPlayerSpawn @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
            return HookResult.Continue;

        AddTimer(2f, () =>
        {
            if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
                return;

            toggleAdminESP[player.Slot] = false;
        });

        return HookResult.Continue;
    }

    public HookResult OnRoundStart(EventRoundStart @event, GameEventInfo info)
    {
        for (int i = 0; i < cachedPlayers.Count; i++)
        {
            var cached = cachedPlayers[i];
            if (cached == null || !cached.IsValid) continue;

            if (toggleAdminESP[cached.Slot] && cached.Team == CsTeam.Spectator && Config.SkipSpectatingEsps)
                continue;

            toggleAdminESP[cached.Slot] = false;
        }

        if (togglePlayersGlowing)
            togglePlayersGlowing = false;

        Server.NextFrame(() =>
        {
            if (!AreThereEsperingAdmins())
            {
                RemoveAllGlowingPlayers();
                return;
            }

            if (AreThereEsperingAdmins() && Config.SkipSpectatingEsps)
                SetAllPlayersGlowing();
        });

        return HookResult.Continue;
    }

    public HookResult OnPlayerDeath(EventPlayerDeath @event, GameEventInfo info)
    {
        var player = @event.Userid;
        if (player == null || !player.IsValid || player.Connected != PlayerConnectedState.PlayerConnected)
            return HookResult.Continue;

        if (glowingPlayers.ContainsKey(player.Slot))
        {
            if (glowingPlayers[player.Slot].Item1 != null && glowingPlayers[player.Slot].Item1.IsValid
            && glowingPlayers[player.Slot].Item2 != null && glowingPlayers[player.Slot].Item2.IsValid)
            {
                glowingPlayers[player.Slot].Item1.AcceptInput("Kill");
                glowingPlayers[player.Slot].Item2.AcceptInput("Kill");
            }
            glowingPlayers.Remove(player.Slot);
        }

        return HookResult.Continue;
    }

    private void OnClientDisconnected(int slot)
    {
        var player = Utilities.GetPlayerFromSlot(slot);
        if (player == null || !player.IsValid) return;

        toggleAdminESP[slot] = false;

        if (cachedPlayers.Contains(player))
            cachedPlayers.Remove(player);
    }
}
