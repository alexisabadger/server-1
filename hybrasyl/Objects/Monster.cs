﻿/*
 * This file is part of Project Hybrasyl.
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the Affero General Public License as published by
 * the Free Software Foundation, version 3.
 *
 * This program is distributed in the hope that it will be useful, but
 * without ANY WARRANTY; without even the implied warranty of MERCHANTABILITY
 * or FITNESS FOR A PARTICULAR PURPOSE. See the Affero General Public License
 * for more details.
 *
 * You should have received a copy of the Affero General Public License along
 * with this program. If not, see <http://www.gnu.org/licenses/>.
 *
 * (C) 2020 ERISCO, LLC 
 *
 * For contributors and individual authors please refer to CONTRIBUTORS.MD.
 * 
 */

using Hybrasyl.Casting;
using Hybrasyl.Enums;
using Hybrasyl.Interfaces;
using Hybrasyl.Messaging;
using Hybrasyl.Scripting;
using Hybrasyl.Xml.Objects;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;

namespace Hybrasyl.Objects;

public enum MobAction
{
    Attack,
    Move,
    Idle,
    Death
}

public class Monster : Creature, ICloneable, IEphemeral
{
    private readonly ConcurrentQueue<MobAction> _actionQueue;
    private readonly object _lock = new();

    private bool _idle = true;

    private uint _mTarget;

    public int ActionDelay = 800;

    public Tile CurrentPath;

    public (int X, int Y) Destination;

    public bool HasCastNearDeath;

    public Loot Loot;

    public SpawnFlags SpawnFlags;

    public byte AssailSound;

    public List<string> Immunities { get; set; }

    public Monster(Xml.Objects.Creature creature, SpawnFlags flags, byte level, Loot loot = null,
        CreatureBehaviorSet behaviorsetOverride = null)
    {
        _actionQueue = new ConcurrentQueue<MobAction>();
        SpawnFlags = flags;
        if (behaviorsetOverride != null)
        {
            BehaviorSet = behaviorsetOverride;
        }
        else if (!string.IsNullOrEmpty(creature.BehaviorSet))
        {
            if (World.WorldData.TryGetValue<CreatureBehaviorSet>(creature.BehaviorSet, out var behaviorSet))
                BehaviorSet = behaviorSet;
            else
                GameLog.SpawnError($"{Name}: behavior set {creature.BehaviorSet} could not be found");
        }

        Stats.BaseInt = 3;
        Stats.BaseDex = 3;
        Stats.BaseStr = 3;
        Stats.BaseWis = 3;
        Stats.BaseCon = 3;
        Stats.BaseAc = 100;

        Name = creature.Name;
        Sprite = creature.Sprite;
        AssailSound = creature.AssailSound;
        Immunities = BehaviorSet?.Immunities ?? new List<string>();

        // TODO: remove this and fix
        Stats.Level = level;
        DisplayText = creature.Description;

        Loot = loot;

        if (flags.HasFlag(SpawnFlags.MovementDisabled))
            ShouldWander = false;

        Hostility = creature.Hostility;
        ThreatInfo = new ThreatInfo(Guid);
        DeathProcessed = false;
        AllocateStats();
        Stats.Hp = Stats.MaximumHp;
        Stats.Mp = Stats.MaximumMp;
        if (BehaviorSet?.Behavior?.SetCookies == null) return;
        foreach (var cookie in BehaviorSet.Behavior.SetCookies.Where(predicate: cookie => !HasCookie(cookie.Name)))
            SetCookie(cookie.Name, cookie.Value);
    }

    public CastableController CastableController { get; set; }
    private CreatureBehaviorSet _behaviorSet { get; set; }
    public CreatureHostilitySettings Hostility { get; set; }
    public DateTime CreationTime { get; set; }
    public double AliveSeconds => (DateTime.Now - CreationTime).TotalSeconds;

    public bool IsHostile(Creature hostile = null)
    {
        // Default to no aggressiveness in the absence of a <Hostility> tag or no <Player> tag;
        // also don't handle monster -> monster combat cases yet
        if (Hostility?.Players == null || hostile is not User user)
        {
            GameLog.SpawnDebug($"Monster {Name}: hostility null or non-user");
            return false;
        }
        // If the creature in question has hit us previously, obviously we don't like it

        if (!string.IsNullOrEmpty(Hostility.Players.ExceptCookie))
        {
            GameLog.SpawnDebug($"Monster {Name}: except cookie");
            return !user.HasCookie(Hostility.Players.ExceptCookie);
        }

        if (!string.IsNullOrEmpty(Hostility.Players.OnlyCookie))
        {
            GameLog.SpawnDebug($"Monster {Name}: only cookie");
            return user.HasCookie(Hostility.Players.OnlyCookie);
        }

        GameLog.SpawnDebug($"Monster {Name}: hostile towards {user.Name}");
        return true;
    }

    public CreatureBehaviorSet BehaviorSet
    {
        get => _behaviorSet;
        set
        {
            if (_behaviorSet == null)
            {
                _behaviorSet = value;
            }
            else
            {
                _behaviorSet = value;
                CastableController.ProcessCastingSets(value.Behavior?.CastingSets ?? new List<CreatureCastingSet>());
            }
        }
    }

    public DateTime LastAction { get; set; } = DateTime.MinValue;
    public DateTime LastSkill { get; set; } = DateTime.MinValue;
    public DateTime LastSpell { get; set; } = DateTime.MinValue;
    public bool ShouldWander { get; set; }
    public bool DeathDisabled => SpawnFlags.HasFlag(SpawnFlags.DeathDisabled);
    public bool MovementDisabled => SpawnFlags.HasFlag(SpawnFlags.MovementDisabled);
    public bool AiDisabled => SpawnFlags.HasFlag(SpawnFlags.AiDisabled);
    public bool DeathProcessed { get; set; }

    public bool ScriptExists { get; set; }

    public ThreatInfo ThreatInfo { get; private set; }

    private bool _active { get; set; }
    public DateTime ActiveSince { get; set; }

    public double ActiveSeconds
    {
        get
        {
            if (ActiveSince != DateTime.MinValue) return (DateTime.Now - ActiveSince).TotalSeconds;
            return -1;
        }
    }

    public bool Active
    {
        get => _active;
        set
        {
            ActiveSince = value == false ? DateTime.MinValue : DateTime.Now;
            _active = value;
        }
    }

    public bool HasAssailSkills { get; set; }

    public uint LootableXP
    {
        get => Loot?.Xp ?? 0;
        set => Loot.Xp = value;
    }

    public uint LootableGold => Loot?.Gold ?? 0;

    public List<string> LootableItems => Loot?.Items ?? new List<string>();

    public Creature Target
    {
        get
        {
            if (World.Objects.TryGetValue(_mTarget, out var o))
                return o as Creature;
            return null;
        }
        set => _mTarget = value?.Id ?? 0;
    }

    public object Clone() => MemberwiseClone();

    // TODO: create "computer controllable object" base class and put this there instead
    public Dictionary<string, dynamic> EphemeralStore { get; set; } = new();
    public object StoreLock { get; } = new();

    public override void OnInsert()
    {
        CastableController = new CastableController(Guid);
        CastableController.LearnCastables();
        CastableController.ProcessCastingSets(BehaviorSet?.Behavior?.CastingSets ?? new List<CreatureCastingSet>());
        ActiveSince = DateTime.Now;
    }

    public bool MeetsRequirement(Requirement req)
    {
        if (req.Physical == null) return true;
        return Stats.Str >= req.Physical.Str && Stats.Int >= req.Physical.Int && Stats.Wis >= req.Physical.Wis &&
               Stats.Con >= req.Physical.Con && Stats.Dex >= req.Physical.Dex;
    }

    public override void OnDeath()
    {
        lock (_lock)
        {
            if (DeathDisabled)
            {
                Stats.Hp = Stats.MaximumHp;
                return;
            }

            // Don't die twice
            if (DeathProcessed) return;

            // Even if we encounter an error, we still count the death as processed to avoid 
            // repeated processing
            DeathProcessed = true;
            _actionQueue.Clear();

            if (!(LastHitter is User hitter))
            {
                Map.Remove(this);
                World.Remove(this);
                GameLog.Error("OnDeath: lasthitter was null");
                return; // Don't handle cases of MOB ON MOB COMBAT just yet
            }

            try
            {
                var deadTime = DateTime.Now;

                if (hitter.Grouped)
                {
                    ItemDropAllowedLooters = hitter.Group.Members.Select(selector: user => user.Name).ToList();
                    hitter.Group.Members.ForEach(action: x => x.TrackKill(Name, deadTime));
                }
                else
                {
                    ItemDropAllowedLooters.Add(hitter.Name);
                    hitter.TrackKill(Name, deadTime);
                }

                hitter.ShareExperience(LootableXP, Stats.Level);
                if (hitter.Stats.ExtraXp > 0)
                    hitter.GiveExperience(LootableXP, true);

                var itemDropTime = DateTime.Now;

                if (LootableGold > 0)
                {
                    var goldObj = new Gold(hitter.CalculateGold(LootableGold))
                    {
                        ItemDropType = ItemDropType.MonsterLootPile,
                        ItemDropAllowedLooters = ItemDropAllowedLooters,
                        ItemDropTime = itemDropTime
                    };
                    World.Insert(goldObj);
                    Map.Insert(goldObj, X, Y);
                }

                foreach (var itemname in LootableItems)
                {
                    var item = Game.World.CreateItem(itemname);
                    if (item == null)
                    {
                        GameLog.UserActivityError("User {player}: looting {monster}, loot item {item} is missing",
                            hitter.Name, Name, itemname);
                        continue;
                    }

                    item.ItemDropType = ItemDropType.MonsterLootPile;
                    item.ItemDropAllowedLooters = ItemDropAllowedLooters;
                    item.ItemDropTime = itemDropTime;
                    World.Insert(item);
                    Map.Insert(item, X, Y);
                }
            }
            catch (Exception e)
            {
                GameLog.Error("OnDeath for {Name}: exception encountered, loot/gold cancelled {e}", Name, e);
                Game.ReportException(e);
            }

            Game.World.RemoveStatusCheck(this);
            // TODO: ondeath castables
            InitScript();
            // FIXME: in the glorious future, run asynchronously with locking
            Script?.ExecuteFunction("OnDeath",
                ScriptEnvironment.Create(("origin", this), ("target", this), ("source", LastHitter)));
            Map?.Remove(this);
            World?.Remove(this);
        }
    }

    // We follow a different pattern here due to the fact that monsters
    // are not intended to be long-lived objects, and we don't want to a
    // spend a lot of overhead and resources creating a full script (eg via
    // OnSpawn) when not needed 99% of the time.
    private void InitScript()
    {
        if (Script != null || ScriptExists || string.IsNullOrEmpty(Name))
            return;

        if (Game.World.ScriptProcessor.TryGetScript(Name, out var damageScript))
        {
            Script = damageScript;
            Script.AssociateScriptWithObject(this);
            ScriptExists = true;
        }
        else
        {
            ScriptExists = false;
        }
    }

    public override bool UseCastable(Castable castableXml, Creature target)
    {
        if (!Condition.CastingAllowed) return false;
        if (castableXml.IsAssail) Motion(1, 20);
        return base.UseCastable(castableXml, target);
    }

    public override void OnHear(SpokenEvent e)
    {
        if (e.Speaker == this)
            return;

        // FIXME: in the glorious future, run asynchronously with locking
        InitScript();
        base.OnHear(e);
    }


    public override void OnDamage(DamageEvent damageEvent)
    {
        lock (_lock)
        {
            if (damageEvent.Source != null && !damageEvent.Flags.HasFlag(DamageFlags.NoThreat))
            {
                if (!ThreatInfo.ContainsThreat(damageEvent.Source))
                    ThreatInfo.AddNewThreat(damageEvent.Source, damageEvent.Amount);
                else
                    ThreatInfo.IncreaseThreat(damageEvent.Source, damageEvent.Amount);
            }

            Condition.Asleep = false;
            ShouldWander = false;

            // FIXME: in the glorious future, run asynchronously with locking
            InitScript();
            if (damageEvent.Source is User user)
                user.SendCombatLogMessage(damageEvent);

            if (Script == null) return;

            var env = ScriptEnvironment.CreateWithOriginTargetAndSource(this, this, damageEvent.Source);
            env.Add("damage", damageEvent);
            Script.ExecuteFunction("OnDamage", env);
        }
    }

    public override void OnHeal(HealEvent healEvent)
    {
        // FIXME: in the glorious future, run asynchronously with locking
        InitScript();
        if (Script == null) return;
        var env = ScriptEnvironment.CreateWithOriginTargetAndSource(healEvent.Source, this, healEvent.Source);
        env.Add("heal", healEvent);
        Script.ExecuteFunction("OnHeal", env);
    }

    public void ApplyModifier(double modifier)
    {
        Stats.BaseHp = (uint)(Stats.BaseHp * (1 + modifier));
        Stats.BaseMp = (uint)(Stats.BaseMp * (1 + modifier));
        LootableXP = (uint)(LootableXP * (1 + modifier));
        if (Loot?.Gold > 0)
            Loot.Gold = (uint)(Loot.Gold * (1 + modifier));
        Stats.BaseOutboundDamageModifier = 1 + modifier;
        Stats.BaseInboundDamageModifier = 1 - modifier;
        Stats.BaseOutboundHealModifier = 1 + modifier;
        Stats.BaseInboundHealModifier = 1 - modifier;
    }

    private void RandomlyAllocateStatPoints(int points)
    {
        // Random allocation
        for (var x = 1; x <= points; x++)
            switch (Random.Shared.Next(1, 6))
            {
                case 1:
                    Stats.BaseStr += 1;
                    break;
                case 2:
                    Stats.BaseInt += 1;
                    break;
                case 3:
                    Stats.BaseDex += 1;
                    break;
                case 4:
                    Stats.BaseCon += 1;
                    break;
                case 5:
                    Stats.BaseWis += 1;
                    break;
            }
    }

    public void AllocateStats()
    {
        var totalPoints = Stats.Level * 2;
        if (BehaviorSet is null || string.IsNullOrEmpty(BehaviorSet.StatAlloc))
        {
            RandomlyAllocateStatPoints(totalPoints);
        }
        else
        {
            var allocPattern = BehaviorSet.StatAlloc.Trim().ToLower().Split(" ");
            while (totalPoints > 0)
                foreach (var alloc in allocPattern)
                {
                    switch (alloc)
                    {
                        case "str":
                            Stats.BaseStr += 1;
                            break;
                        case "int":
                            Stats.BaseInt += 1;
                            break;
                        case "wis":
                            Stats.BaseWis += 1;
                            break;
                        case "con":
                            Stats.BaseCon += 1;
                            break;
                        case "dex":
                            Stats.BaseDex += 1;
                            break;
                        default:
                            RandomlyAllocateStatPoints(1);
                            break;
                    }

                    totalPoints--;
                    if (totalPoints % 2 == 0)
                    {
                        var randomBonus = Random.Shared.NextDouble() * 0.30 + 0.85;
                        var bonusHpGain =
                            (int)Math.Ceiling((double)(Stats.BaseCon / (float)Stats.Level) * 50 * randomBonus);
                        var bonusMpGain =
                            (int)Math.Ceiling((double)(Stats.BaseWis / (float)Stats.Level) * 50 * randomBonus);

                        Stats.BaseHp += bonusHpGain + 25;
                        Stats.BaseMp += bonusMpGain + 25;
                    }
                }
        }

        Stats.Hp = Stats.MaximumHp;
        Stats.Mp = Stats.MaximumMp;
    }

    public override int GetHashCode() => Name.GetHashCode() * Id.GetHashCode() - 1;

    public bool CheckFacing(Direction direction, Creature target)
    {
        if (target == null) return false;
        if (Math.Abs(X - target.X) <= 1 && Math.Abs(Y - target.Y) <= 1)
        {
            if (X - target.X == 1 && Y - target.Y == 0)
            {
                //check if facing west
                if (Direction == Direction.West) return true;
                Turn(Direction.West);
            }

            if (X - target.X == -1 && Y - target.Y == 0)
            {
                //check if facing east
                if (Direction == Direction.East) return true;
                Turn(Direction.East);
            }

            if (X - target.X == 0 && Y - target.Y == 1)
            {
                //check if facing south
                if (Direction == Direction.North) return true;
                Turn(Direction.North);
            }

            if (X - target.X == 0 && Y - target.Y == -1)
            {
                if (Direction == Direction.South) return true;
                Turn(Direction.South);
            }
        }

        return false;
    }

    /// <summary>
    ///     A simple attack by a monster (equivalent of straight assail).
    /// </summary>
    /// <param name="target"></param>
    public void AssailAttack(Direction direction, Creature target = null)
    {
        if (target == null)
        {
            var obj = GetDirectionalTarget(direction);
            if (obj is Merchant)
                return;
            if (obj is Creature || obj is User)
                target = obj;
        }

        if (target == null)
            return;
        if (!CastableController.TryGetCastable("Assail", out var slot)) return;
        UseCastable(slot.Castable, target);
        Motion(1, 20);
        PlaySound(1);
    }

    public override void ShowTo(IVisible obj)
    {
        if (obj is not User user) return;
        if (!Condition.IsInvisible || user.Condition.SeeInvisible)
            user.SendVisibleCreature(this);
    }

    public bool IsIdle() => _idle;

    public void Awaken()
    {
        _idle = false;
        //add to alive monsters?
    }

    public void Sleep()
    {
        _idle = true;
        //return to idle state
    }

    public List<Tile> GetWalkableTiles(int x, int y)
    {
        var proposedLocations = new List<Tile>
        {
            new() { X = x, Y = y - 1 },
            new() { X = x, Y = y + 1 },
            new() { X = x - 1, Y = y },
            new() { X = x + 1, Y = y }
        };

        // Don't return tiles that are walls, or tiles that contain creatures, but always
        // return our end tile

        var ret = new List<Tile>();

        foreach (var adj in proposedLocations)
        {
            if (adj.X >= Map.X || adj.Y >= Map.Y || adj.X < 0 || adj.Y < 0) continue;
            if (Map.IsWall(adj.X, adj.Y)) continue;
            var creatureContents = Map.GetCreatures(adj.X, adj.Y);
            if (creatureContents.Count == 0 || creatureContents.Contains(Target) || creatureContents.Contains(this))
                ret.Add(adj);
        }

        return ret;
    }

    private static int AStarCalculateH(int x1, int y1, int x2, int y2) => Math.Abs(x2 - x1) + Math.Abs(y2 - y1);

    public Direction AStarGetDirection()
    {
        if (CurrentPath.Parent == null) return Direction.North;
        var dir = Direction.North;

        if (X == CurrentPath.Parent.X)
        {
            if (CurrentPath.Parent.Y == Y + 1) dir = Direction.South;
            if (CurrentPath.Parent.Y == Y - 1) dir = Direction.North;
        }
        else if (Y == CurrentPath.Parent.Y)
        {
            if (CurrentPath.Parent.X == X + 1) dir = Direction.East;
            if (CurrentPath.Parent.X == X - 1) dir = Direction.West;
        }
        else
        {
            GameLog.Warning("AStar: path divergence, moving randomly");
        }

        return dir;
    }

    /// <summary>
    ///     Verify that the next two steps of our path can be used.
    /// </summary>
    /// <returns>Boolean indicating whether the immediate path is clear or not.</returns>
    public bool AStarPathClear()
    {
        if (CurrentPath == null) return true;
        // TODO: optimize
        if (Map.IsCreatureAt(CurrentPath.X, CurrentPath.Y) && CurrentPath.Parent != null &&
            Map.IsCreatureAt(CurrentPath.Parent.X, CurrentPath.Parent.Y))
            if (!(X == CurrentPath.X && Y == CurrentPath.Y) || X == CurrentPath.Parent.X || Y == CurrentPath.Parent.Y)
            {
                GameLog.Info(
                    $"AStar: path not clear at either {CurrentPath.X}, {CurrentPath.Y} or {CurrentPath.Parent.X}, {CurrentPath.Parent.Y}");
                return false;
            }

        return true;
    }

    public Tile AStarPathFind(int x1, int y1, int x2, int y2)
    {
        GameLog.Info($"AStarPath: from {x1},{y1} to {x2},{y2}");
        Tile current = null;
        var start = new Tile { X = x1, Y = y1 };
        var end = new Tile { X = x2, Y = y2 };

        var openList = new List<Tile>();
        var closedList = new List<Tile>();
        var g = 0;

        openList.Add(start);

        while (openList.Count > 0)
        {
            var lowest = openList.Min(selector: l => l.F);
            current = openList.First(predicate: l => l.F == lowest);

            closedList.Add(current);
            openList.Remove(current);

            if (closedList.FirstOrDefault(predicate: l => l.X == end.X && l.Y == end.Y) != null)
            {
                // We have arrived
                GameLog.Info($"Closed list contains end tile {end.X}, {end.Y}");
                break;
            }

            var adj = GetWalkableTiles(current.X, current.Y);
            if (adj.Count == 0)
                GameLog.Warning("Adjacent tiles: 0");
            g++;

            foreach (var tile in adj)
            {
                // Ignore tiles in closed list
                if (closedList.FirstOrDefault(predicate: l => l.X == tile.X && l.Y == tile.Y) != null)
                    continue;

                //GameLog.Debug($"Adjacencies: {tile.X}, {tile.Y}");

                if (openList.FirstOrDefault(predicate: l => l.X == tile.X && l.Y == tile.Y) == null)
                {
                    tile.G = g;
                    tile.H = AStarCalculateH(tile.X, tile.Y, end.X, end.Y);
                    tile.F = tile.G + tile.H;
                    tile.Parent = current;
                    openList.Insert(0, tile);
                    //GameLog.Debug($"Adding {tile.X}, {tile.Y} to the open list");
                }
                else
                {
                    if (g + tile.H < tile.F)
                    {
                        tile.G = g;
                        tile.F = tile.G + tile.H;
                        tile.Parent = current;
                    }
                }
            }
        }

        // If null here, no path was found
        if (current != null)
            // Save our coordinate target for future reference
            current.Target = (x1, y1);
        else
            GameLog.Debug("AStar path find: no path found");
        return current;
    }

    public Direction Relation(int x1, int y1)
    {
        if (Y > y1)
            return Direction.North;
        if (X < x1)
            return Direction.East;
        if (Y < y1)
            return Direction.South;
        if (X > x1)
            return Direction.West;
        return Direction.North;
    }

    public void Cast(BookSlot slot, Creature target)
    {
        if (!Condition.CastingAllowed) return;
        Condition.Casting = true;
        UseCastable(slot.Castable, target);
        Condition.Casting = false;
        slot.LastCast = DateTime.Now;
        slot.UseCount++;
    }

    public void Attack()
    {
        if (ThreatInfo.HighestThreat == null) return;
        if (CheckFacing(Direction, ThreatInfo.HighestThreat))
            AssailAttack(Direction, ThreatInfo.HighestThreat);
        else
            Turn(Relation(ThreatInfo.HighestThreat.X, ThreatInfo.HighestThreat.Y));
    }


    public void NextAction()
    {
        var next = 0;
        if (Stats.Hp == 0)
        {
            _actionQueue.Enqueue(MobAction.Death);
            return;
        }

        if (ThreatInfo.HighestThreat != null)
        {
            if (Distance(ThreatInfo.HighestThreat) == 1)
            {
                _actionQueue.Enqueue(MobAction.Attack);
            }
            else
            {
                next = Random.Shared.Next(1, 3); //cast or move
                _actionQueue.Enqueue((MobAction)next);
            }
        }
        else
        {
            _actionQueue.Enqueue(MobAction.Move);
        }
    }

    public void ProcessActions()
    {
        while (!_actionQueue.IsEmpty)
        {
            _actionQueue.TryDequeue(out var action);
            GameLog.SpawnDebug($"ActionQueue: {action}");
            switch (action)
            {
                case MobAction.Attack:
                    var next = CastableController.GetNextCastable();
                    if (next is null)
                    {
                        Attack();
                        return;
                    }

                    var targets = ThreatInfo.GetTargets(next.CurrentPriority);
                    if (targets.Count == 0)
                    {
                        GameLog.SpawnDebug(
                            $"{Name}: ({Map.Name}@{X},{Y}): no targets returned from priority {next.CurrentPriority}");
                        return;
                    }

                    if (targets.Count == 1 && next.Slot.Castable.IsAssail)
                    {
                        if (Distance(ThreatInfo.HighestThreat) > 1)
                        {
                            _actionQueue.Enqueue(MobAction.Move);
                            return;
                        }

                        if (!CheckFacing(Direction, ThreatInfo.HighestThreat))
                            Turn(Relation(ThreatInfo.HighestThreat.X, ThreatInfo.HighestThreat.Y));
                    }

                    foreach (var target in targets) Cast(next.Slot, target);

                    return;
                case MobAction.Move when !Condition.MovementAllowed:
                    return;
                case MobAction.Move when ShouldWander || Condition.Blinded:
                    {
                        var rand = Random.Shared.NextDouble();
                        var dir = Random.Shared.Next(0, 4);
                        if (rand > 0.33)
                        {
                            if (Direction == (Direction)dir)
                                if (!Walk(Direction))
                                {
                                    Turn(Opposite(Direction));
                                    Walk(Opposite(Direction));
                                }
                                else
                                    Turn((Direction)dir);
                        }
                        else
                            Walk(Direction);

                        break;
                    }
                case MobAction.Move:
                    {
                        if (ThreatInfo.HighestThreat == null)
                        {
                            ShouldWander = true;
                            return;
                        }

                        if (Condition.MovementAllowed)
                        {
                            if (CurrentPath == null || !AStarPathClear())
                            // If we don't have a current path to our threat target, OR if there is something in the way of
                            // our existing path, calculate a new one
                            {
                                if (CurrentPath == null) GameLog.Info("Path is null. Recalculating");
                                if (!AStarPathClear()) GameLog.Info("Path wasn't clear. Recalculating");
                                Target = ThreatInfo.HighestThreat;
                                CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X,
                                    ThreatInfo.HighestThreat.Location.Y, X, Y);
                            }

                            if (CurrentPath != null)
                            {
                                // We have a path, check its validity
                                // We recalculate our path if we're within five spaces of the target and they have moved

                                if (Distance(ThreatInfo.HighestThreat) < 5 &&
                                    CurrentPath.Target.X != ThreatInfo.HighestThreat.Location.X &&
                                    CurrentPath.Target.Y != ThreatInfo.HighestThreat.Location.Y)
                                {
                                    GameLog.Info("Distance less than five and target moved, recalculating path");
                                    CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X,
                                        ThreatInfo.HighestThreat.Location.Y, X, Y);
                                }

                                if (Walk(AStarGetDirection()))
                                {
                                    if (X != CurrentPath.X || Y != CurrentPath.Y)
                                        GameLog.SpawnError(
                                            $"Walk: followed astar path but not on path (at {X},{Y} path is {CurrentPath.X}, {CurrentPath.Y}");
                                    // We've moved; update our path
                                    CurrentPath = CurrentPath.Parent;
                                }
                                else
                                // Couldn't move, attempt to recalculate path
                                {
                                    CurrentPath = AStarPathFind(ThreatInfo.HighestThreat.Location.X,
                                        ThreatInfo.HighestThreat.Location.Y, X, Y);
                                }
                            }
                            else
                            // If we can't find a path, return to wandering
                            {
                                ShouldWander = true;
                            }
                        }
                        else
                        {
                            GameLog.SpawnError("Can't move");
                        }

                        break;
                    }
                case MobAction.Idle:
                    //do nothing
                    break;
                case MobAction.Death:
                    _actionQueue.Clear();
                    break;
            }
        }
    }

    public override void AoiDeparture(VisibleObject obj)
    {
        lock (_lock)
        {
            if (obj is User user)
            {
                ThreatInfo.OnRangeExit(user);

                if (ThreatInfo.HighestThreat == null && ThreatInfo.Count == 0)
                {
                    ShouldWander = true;
                    FirstHitter = null;
                    Target = null;
                    Stats.Hp = Stats.MaximumHp;
                }
            }

            if (Map.EntityTree.GetObjects(GetViewport()).OfType<User>().ToList().Count == 0) Active = false;

            base.AoiDeparture(obj);
        }
    }

    public override void AoiEntry(VisibleObject obj)
    {
        lock (_lock)
        {
            if (obj is User user && (!user.Condition.IsInvisible || Condition.SeeInvisible))
            {
                if (Map.EntityTree.GetObjects(GetViewport()).OfType<User>().ToList().Count > 0) Active = true;

                if (IsHostile(user) && ThreatInfo.HighestThreat == null)
                {
                    ThreatInfo.OnRangeEnter(user);
                    ShouldWander = false;
                }
            }

            base.AoiEntry(obj);
        }
    }

    public List<Creature> GetThreatTarget(CreatureTargetPriority priority) => ThreatInfo.GetTargets(priority);
}