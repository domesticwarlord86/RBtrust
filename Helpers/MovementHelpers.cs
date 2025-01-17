﻿using Buddy.Coroutines;
using Clio.Utilities;
using ff14bot;
using ff14bot.Managers;
using ff14bot.Navigation;
using ff14bot.Objects;
using System;
using System.Linq;
using System.Threading.Tasks;
using Trust.Data;

namespace Trust.Helpers;

/// <summary>
/// Miscellaneous functions related to movement.
/// </summary>
internal static class MovementHelpers
{
    /// <summary>
    /// Gets the nearest party member.
    /// </summary>
    public static BattleCharacter GetClosestAlly => GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
        .Where(obj => !obj.IsDead && PartyMembers.SafePartyMemberIds.Contains((PartyMemberId)obj.NpcId))
        .OrderBy(r => r.Distance())
        .FirstOrDefault();

    /// <summary>
    /// Gets the furthest Ally from the Player.
    /// </summary>
    public static BattleCharacter GetFurthestAlly => GameObjectManager
        .GetObjectsOfType<BattleCharacter>(true, false)
        .Where(obj => !obj.IsDead && PartyMembers.SafePartyMemberIds.Contains((PartyMemberId)obj.NpcId))
        .OrderByDescending(r => r.Distance())
        .FirstOrDefault();

    /// <summary>
    /// Gets the nearest DPS party member.
    /// </summary>
    public static BattleCharacter GetClosestDps => GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
        .Where(obj => !obj.IsDead && PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId) &&
                      ClassJobRoles.DPS.Contains(obj.CurrentJob))
        .OrderBy(r => r.Distance())
        .FirstOrDefault();

    /// <summary>
    /// Gets the nearest Tank party member.
    /// </summary>
    public static BattleCharacter GetClosestTank => GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
        .Where(obj => !obj.IsDead && PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId) &&
                      ClassJobRoles.Tanks.Contains(obj.CurrentJob))
        .OrderBy(r => r.Distance())
        .FirstOrDefault();

    /// <summary>
    /// Gets the nearest melee party member.
    /// </summary>
    public static BattleCharacter GetClosestMelee => GameObjectManager
        .GetObjectsOfType<BattleCharacter>(true, false)
        .Where(obj => !obj.IsDead && PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId) &&
                      ClassJobRoles.Melee.Contains(obj.CurrentJob))
        .OrderBy(r => r.Distance())
        .FirstOrDefault();

    /// <summary>
    /// Gets the nearest party member to a point.
    /// </summary>
    /// <param name="location">Point to measure from.</param>
    /// <returns>Nearest party member.</returns>
    public static BattleCharacter GetClosestPartyMember(Vector3 location)
    {
        if (location == null)
        {
            return null;
        }

        return GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
            .Where(obj => !obj.IsDead && PartyMembers.SafePartyMemberIds.Contains((PartyMemberId)obj.NpcId))
            .OrderBy(r => r.Distance(location))
            .FirstOrDefault();
    }

    /// <summary>
    /// Avoids all party members for a certain amount of time.
    /// </summary>
    /// <param name="timeToSpread">Spread duration, in milliseconds.</param>
    /// <param name="spreadDistance">Minimum distance between party members.</param>
    /// <returns><see langword="true"/> if this behavior expected/handled execution.</returns>
    public static async Task<bool> Spread(double timeToSpread, float spreadDistance = 6.5f)
    {
        double currentMS = DateTime.Now.TimeOfDay.TotalMilliseconds;
        double endMS = currentMS + timeToSpread;

        if (!AvoidanceManager.IsRunningOutOfAvoid)
        {
            foreach (BattleCharacter npc in PartyManager.AllMembers.Select(p => p.BattleCharacter)
                         .OrderByDescending(obj => Core.Player.Distance(obj)))
            {
                AvoidanceManager.AddAvoidObject<BattleCharacter>(
                    () => DateTime.Now.TimeOfDay.TotalMilliseconds <= endMS && DateTime.Now.TimeOfDay.TotalMilliseconds >= currentMS,
                    radius: spreadDistance,
                    npc.ObjectId);
            }

            await Coroutine.Wait(300, () => AvoidanceManager.IsRunningOutOfAvoid);
        }

        if (!AvoidanceManager.IsRunningOutOfAvoid)
        {
            MovementManager.MoveStop();
        }

        return true;
    }

    public static async Task<bool> HalfSpread(double timeToSpread, float spreadDistance = 6.5f, uint spbc = 0)
    {
        double currentMS = DateTime.Now.TimeOfDay.TotalMilliseconds;
        double endMS = currentMS + timeToSpread;

        if (spbc != 0)
        {
            BattleCharacter closestPartyMember = PartyManager.AllMembers
                .Select(pm => pm.BattleCharacter)
                .OrderBy(obj => obj.Distance(Core.Player))
                .FirstOrDefault(obj => !obj.IsMe);

            GameObject target = Core.Player.CurrentTarget;

            LocalPlayer player = Core.Player;

            // This appears to be "equation of line" y = mx + b, but z instead of y (height)
            Vector3 newLoc = default;
            if (target != null && player != null && target.Distance2D(player) > 0)
            {
                float m = (target.Z - player.Z) / (target.X - player.X);
                float b = target.Z - (m * target.X);

                float plg = 100f / player.DistanceSqr(target.Location);

                newLoc.X = player.X - (plg * (target.X - player.X));
                newLoc.Z = (m * newLoc.X) + b;
                newLoc.Y = target.Y;

                if (closestPartyMember.Distance(newLoc) - 2f < Core.Player.Distance(newLoc))
                {
                    Navigator.PlayerMover.MoveTowards(newLoc);
                    await Coroutine.Yield();
                    return false;
                }
            }
        }

        foreach (BattleCharacter npc in GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
                     .Where(obj => PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId))
                     .OrderByDescending(obj => Core.Player.Distance(obj)))
        {
            AvoidanceManager.AddAvoidObject<BattleCharacter>(
                () => DateTime.Now.TimeOfDay.TotalMilliseconds <= endMS && DateTime.Now.TimeOfDay.TotalMilliseconds >= currentMS,
                radius: spreadDistance,
                npc.ObjectId);

            await Coroutine.Yield();
        }

        if (!AvoidanceManager.IsRunningOutOfAvoid)
        {
            MovementManager.MoveStop();
        }

        return true;
    }

    public static async Task<bool> SpreadSp(double timeToSpread, Vector3 vector, float spreadDistance = 6.5f)
    {
        double currentMS = DateTime.Now.TimeOfDay.TotalMilliseconds;
        double endMS = currentMS + timeToSpread;

        BattleCharacter nobj = GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
            .Where(obj => PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId))
            .OrderBy(obj => obj.Distance(Core.Player))
            .FirstOrDefault();

        Vector3 playerLoc = Core.Player.Location;

        float ls = 0;
        if (vector != null)
        {
            if (playerLoc.X > vector.X)
            {
                ls = -20;
            }
            else
            {
                ls = 20;
            }
        }

        foreach (BattleCharacter npc in GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
            .Where(obj => PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId))
            .OrderByDescending(r => Core.Player.Distance()))
        {
            AvoidanceManager.AddAvoidObject<BattleCharacter>(
                () => DateTime.Now.TimeOfDay.TotalMilliseconds <= endMS && DateTime.Now.TimeOfDay.TotalMilliseconds >= currentMS,
                () => new Vector3(playerLoc.X - ls, playerLoc.Y, playerLoc.Z),
                leashRadius: 40,
                radius: spreadDistance,
                npc.ObjectId);

            await Coroutine.Yield();
        }

        if (!AvoidanceManager.IsRunningOutOfAvoid)
        {
            MovementManager.MoveStop();
        }

        return true;
    }

    public static async Task<bool> SpreadSpLoc(double timeToSpread, Vector3 vector, float spreadDistance = 6.5f)
    {
        double currentMS = DateTime.Now.TimeOfDay.TotalMilliseconds;
        double endMS = currentMS + timeToSpread;

        if (vector == null)
        {
            vector = GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
                .Where(obj => PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId))
                .OrderBy(obj => obj.Distance(Core.Player))
                .FirstOrDefault()
                .Location;
        }

        foreach (BattleCharacter npc in GameObjectManager.GetObjectsOfType<BattleCharacter>(true, false)
                     .Where(obj => PartyMembers.AllPartyMemberIds.Contains((PartyMemberId)obj.NpcId))
                     .OrderByDescending(r => Core.Player.Distance()))
        {
            AvoidanceManager.AddAvoidObject<BattleCharacter>(
                () => DateTime.Now.TimeOfDay.TotalMilliseconds <= endMS && DateTime.Now.TimeOfDay.TotalMilliseconds >= currentMS,
                () => vector,
                leashRadius: 40,
                radius: spreadDistance,
                npc.ObjectId);

            await Coroutine.Yield();
        }

        if (!AvoidanceManager.IsRunningOutOfAvoid)
        {
            MovementManager.MoveStop();
        }

        return true;
    }
}
