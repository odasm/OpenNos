﻿/*
 * This file is part of the OpenNos Emulator Project. See AUTHORS file for Copyright information
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 */

using OpenNos.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace OpenNos.GameObject
{
    public class MapMonster : MapMonsterDTO
    {
        #region Members

        private int _movetime;

        #endregion

        #region Instantiation

        public MapMonster(MapMonsterDTO monsterdto, Map parent)
        {
            // Replace by MAPPING
            MapId = monsterdto.MapId;
            MapX = monsterdto.MapX;
            MapMonsterId = monsterdto.MapMonsterId;
            MonsterVNum = monsterdto.MonsterVNum;
            MapY = monsterdto.MapY;
            Position = monsterdto.Position;
            FirstX = monsterdto.MapX;
            FirstY = monsterdto.MapY;
            IsMoving = monsterdto.IsMoving;
            ///////////////////////////////////

            LastEffect = LastMove = DateTime.Now;
            Target = -1;
            Path = new List<MapCell>();
            Map = parent;
            Alive = true;
            Respawn = true;
            Monster = ServerManager.GetNpc(MonsterVNum);
            CurrentHp = Monster.MaxHP;
            CurrentMp = Monster.MaxMP;
            Skills = Monster.Skills.ToList();
            DamageList = new Dictionary<long, long>();
            _movetime = ServerManager.Instance.Random.Next(300, 3000);
        }

        #endregion

        #region Properties

        public bool Alive { get; set; }

        public int CurrentHp { get; set; }

        public int CurrentMp { get; set; }

        public IDictionary<long, long> DamageList { get; set; }

        public DateTime Death { get; set; }

        public short FirstX { get; set; }

        public short FirstY { get; set; }

        public bool InWaiting { get; set; }

        public DateTime LastEffect { get; set; }

        public DateTime LastMove { get; set; }

        public Map Map { get; set; }

        public NpcMonster Monster { get; set; }

        public List<MapCell> Path { get; set; }

        public bool Respawn { get; set; }

        public List<NpcMonsterSkill> Skills { get; set; }

        public long Target { get; set; }

        #endregion

        #region Methods

        public static int GenerateMapMonsterId()
        {
            List<int> monsterIds = new List<int>();

            for (int i = ServerManager.Monsters.Count - 1; i >= 0; i--)
            {
                monsterIds.Add(ServerManager.Monsters[i].MapMonsterId);
            }

            for (int i = 20000; i < int.MaxValue; i++)
            {
                if (!monsterIds.Contains(i))
                {
                    return i;
                }
            }
            return -1;
        }

        public string GenerateEff(int effect)
        {
            return $"eff 3 {MapMonsterId} {effect}";
        }

        public string GenerateIn3()
        {
            if (Alive && !IsDisabled)
            {
                return $"in 3 {MonsterVNum} {MapMonsterId} {MapX} {MapY} {Position} {(int)(((float)CurrentHp / (float)Monster.MaxHP) * 100)} {(int)(((float)CurrentMp / (float)Monster.MaxMP) * 100)} 0 0 0 -1 1 0 -1 - 0 -1 0 0 0 0 0 0 0 0";
            }
            else
            {
                return String.Empty;
            }
        }

        internal void MonsterLife()
        {
            // Respawn
            if (!Alive && Respawn)
            {
                double timeDeath = (DateTime.Now - Death).TotalSeconds;
                if (timeDeath >= Monster.RespawnTime / 10)
                {
                    DamageList = new Dictionary<long, long>();
                    Alive = true;
                    Target = -1;
                    CurrentHp = Monster.MaxHP;
                    CurrentMp = Monster.MaxMP;
                    MapX = FirstX;
                    MapY = FirstY;
                    Path = new List<MapCell>();
                    Map.Broadcast(GenerateIn3());
                    Map.Broadcast(GenerateEff(7));
                }
                return;
            }
            else if (Target == -1)
            {
                // Normal Move Mode
                if (Alive == false)
                {
                    return;
                }
                if (IsMoving && Monster.Speed > 0)
                {
                    double time = (DateTime.Now - LastMove).TotalMilliseconds;

                    if (Path.Where(s => s != null).ToList().Count > 0)
                    {
                        int timetowalk = 1000 / (2 * Monster.Speed);
                        if (time > timetowalk)
                        {
                            short mapX = Path.ElementAt(0).X;
                            short mapY = Path.ElementAt(0).Y;
                            Path.RemoveAt(0);
                            LastMove = DateTime.Now;
                            Map.Broadcast($"mv 3 {this.MapMonsterId} {this.MapX} {this.MapY} {Monster.Speed}");

                            Task.Factory.StartNew(async () =>
                            {
                                await Task.Delay(timetowalk);
                                MapX = mapX;
                                MapY = mapY;
                            });
                            return;
                        }
                    }
                    else if (time > _movetime)
                    {
                        _movetime = ServerManager.Instance.Random.Next(500, 3000);
                        byte point = (byte)ServerManager.Instance.Random.Next(2, 4);
                        byte fpoint = (byte)ServerManager.Instance.Random.Next(0, 2);

                        byte xpoint = (byte)ServerManager.Instance.Random.Next(fpoint, point);
                        byte ypoint = (byte)(point - xpoint);

                        short mapX = FirstX;
                        short mapY = FirstY;
                        if (ServerManager.GetMap(MapId).GetFreePosition(ref mapX, ref mapY, xpoint, ypoint))
                        {
                            Task.Factory.StartNew(async () =>
                            {
                                await Task.Delay(1000 * (xpoint + ypoint) / (2 * Monster.Speed));
                                this.MapX = mapX;
                                this.MapY = mapY;
                            });
                            LastMove = DateTime.Now.AddSeconds((xpoint + ypoint) / (2 * Monster.Speed));

                            string movePacket = $"mv 3 {this.MapMonsterId} {mapX} {mapY} {Monster.Speed}";
                            Map.Broadcast(movePacket);
                        }
                    }
                }
                if (Monster.IsHostile)
                {
                    Character character = ServerManager.Instance.Sessions.FirstOrDefault(s => s != null && s.Character != null && s.Character.Hp > 0 && !s.Character.InvisibleGm && !s.Character.Invisible && s.Character.MapId == MapId && Map.GetDistance(new MapCell() { X = MapX, Y = MapY }, new MapCell() { X = s.Character.MapX, Y = s.Character.MapY }) < 10)?.Character;
                    if (character != null)
                    {
                        Target = character.CharacterId;
                        if (!Monster.NoAggresiveIcon)
                        {
                            character.Session.SendPacket(GenerateEff(5000));
                        }
                    }
                }
            }
            else
            {
                ClientSession targetSession = Map.Sessions.SingleOrDefault(s => s.Character.CharacterId == Target);

                if (targetSession == null || targetSession.Character.Invisible)
                {
                    Target = -1;
                    return;
                }
                NpcMonsterSkill npcMonsterSkill = null;
                if (ServerManager.Instance.Random.Next(10) > 8 || InWaiting)
                {
                    InWaiting = false;
                    if ((DateTime.Now - LastEffect).TotalMilliseconds < Monster.BasicCooldown * 200)
                    {
                        InWaiting = true;
                    }
                    npcMonsterSkill = Skills.Where(s => (DateTime.Now - s.LastUse).TotalMilliseconds >= 100 * s.Skill.Cooldown).OrderBy(rnd => ServerManager.Instance.Random.Next()).FirstOrDefault();
                }

                int damage = 100;

                if (targetSession != null && targetSession.Character.Hp > 0 && ((npcMonsterSkill != null && CurrentMp - npcMonsterSkill.Skill.MpCost >= 0 && Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY }) < npcMonsterSkill.Skill.Range) || (Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY }) <= Monster.BasicRange)))
                {
                    if ((DateTime.Now - LastEffect).TotalMilliseconds >= Monster.BasicCooldown * 200 && !InWaiting)
                    {
                        if (npcMonsterSkill != null)
                        {
                            npcMonsterSkill.LastUse = DateTime.Now;
                            CurrentMp -= npcMonsterSkill.Skill.MpCost;
                            Map.Broadcast($"ct 3 {MapMonsterId} 1 {Target} {npcMonsterSkill.Skill.CastAnimation} {npcMonsterSkill.Skill.CastEffect} {npcMonsterSkill.Skill.SkillVNum}");
                        }
                        LastMove = DateTime.Now;

                        // deal 0 damage to GM with GodMode
                        damage = targetSession.Character.HasGodMode ? 0 : 100;
                        if (targetSession.Character.IsSitting)
                        {
                            targetSession.Character.IsSitting = false;
                            Map.Broadcast(targetSession.Character.GenerateRest());
                            Thread.Sleep(500);
                        }
                        if (npcMonsterSkill != null && npcMonsterSkill.Skill.CastEffect != 0)
                        {
                            Map.Broadcast(GenerateEff(npcMonsterSkill.Skill.CastEffect));
                            Thread.Sleep(npcMonsterSkill.Skill.CastTime * 100);
                        }
                        Path = new List<MapCell>();
                        targetSession.Character.LastDefence = DateTime.Now;
                        targetSession.Character.GetDamage(damage);

                        Map.Broadcast(null, ServerManager.Instance.GetUserMethod<string>(Target, "GenerateStat"), ReceiverType.OnlySomeone, "", Target);

                        if (npcMonsterSkill != null)
                        {
                            Map.Broadcast($"su 3 {MapMonsterId} 1 {Target} {npcMonsterSkill.SkillVNum} {npcMonsterSkill.Skill.Cooldown} {npcMonsterSkill.Skill.AttackAnimation} {npcMonsterSkill.Skill.Effect} {this.MapX} {this.MapY} {(targetSession.Character.Hp > 0 ? 1 : 0)} { (int)(targetSession.Character.Hp / targetSession.Character.HPLoad() * 100) } {damage} 0 0");
                        }
                        else
                        {
                            Map.Broadcast($"su 3 {MapMonsterId} 1 {Target} 0 {Monster.BasicCooldown} 11 {Monster.BasicSkill} 0 0 {(targetSession.Character.Hp > 0 ? 1 : 0)} { (int)(targetSession.Character.Hp / targetSession.Character.HPLoad() * 100) } {damage} 0 0");
                        }

                        LastEffect = DateTime.Now;
                        if (targetSession.Character.Hp <= 0)
                        {
                            Thread.Sleep(1000);
                            ServerManager.Instance.AskRevive(targetSession.Character.CharacterId);
                            Target = -1;
                        }
                        if (npcMonsterSkill != null && (npcMonsterSkill.Skill.Range > 0 || npcMonsterSkill.Skill.TargetRange > 0))
                        {
                            foreach (Character chara in ServerManager.GetMap(MapId).GetListPeopleInRange(npcMonsterSkill.Skill.TargetRange == 0 ? this.MapX : targetSession.Character.MapX, npcMonsterSkill.Skill.TargetRange == 0 ? this.MapY : targetSession.Character.MapY, (byte)(npcMonsterSkill.Skill.TargetRange + npcMonsterSkill.Skill.Range)).Where(s => s.CharacterId != Target && s.Hp > 0))
                            {
                                if (chara.IsSitting)
                                {
                                    chara.IsSitting = false;
                                    Map.Broadcast(chara.GenerateRest());
                                    Thread.Sleep(500);
                                }
                                damage = chara.HasGodMode ? 0 : 100;
                                bool AlreadyDead2 = chara.Hp <= 0;
                                chara.GetDamage(damage);
                                chara.LastDefence = DateTime.Now;
                                Map.Broadcast(null, chara.GenerateStat(), ReceiverType.OnlySomeone, "", chara.CharacterId);
                                Map.Broadcast($"su 3 {MapMonsterId} 1 {chara.CharacterId} 0 {Monster.BasicCooldown} 11 {Monster.BasicSkill} 0 0 {(chara.Hp > 0 ? 1 : 0)} { (int)(chara.Hp / chara.HPLoad() * 100) } {damage} 0 0");
                                if (chara.Hp <= 0 && !AlreadyDead2)
                                {
                                    Thread.Sleep(1000);
                                    ServerManager.Instance.AskRevive(chara.CharacterId);
                                }
                            }
                        }
                    }
                }
                else
                {
                    int distance = Map.GetDistance(new MapCell() { X = this.MapX, Y = this.MapY }, new MapCell() { X = targetSession.Character.MapX, Y = targetSession.Character.MapY });

                    if (IsMoving == true)
                    {
                        short maxDistance = 22;
                        if (Path.Count() == 0 && targetSession != null && distance > 1 && distance < maxDistance)
                        {
                            short xoffset = (short)ServerManager.Instance.Random.Next(-1, 1);
                            short yoffset = (short)ServerManager.Instance.Random.Next(-1, 1);

                            Path = ServerManager.GetMap(MapId).StraightPath(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = (short)(targetSession.Character.MapX + xoffset), Y = (short)(targetSession.Character.MapY + yoffset), MapId = this.MapId });
                            if (!Path.Any())
                            {
                                Path = ServerManager.GetMap(MapId).JPSPlus(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = (short)(targetSession.Character.MapX + xoffset), Y = (short)(targetSession.Character.MapY + yoffset), MapId = this.MapId });
                            }
                        }
                        if (DateTime.Now > LastMove && Monster.Speed > 0 && Path.Count > 0)
                        {
                            short mapX;
                            short mapY;
                            int maxindex = Path.Count > Monster.Speed / 2 ? Monster.Speed / 2 : Path.Count;
                            mapX = Path.ElementAt(maxindex - 1).X;
                            mapY = Path.ElementAt(maxindex - 1).Y;
                            double waitingtime = (double)(Map.GetDistance(new MapCell() { X = mapX, Y = mapY, MapId = MapId }, new MapCell() { X = MapX, Y = MapY, MapId = MapId })) / (double)(Monster.Speed);
                            Map.Broadcast($"mv 3 {this.MapMonsterId} {mapX} {mapY} {Monster.Speed}");
                            LastMove = DateTime.Now.AddSeconds((waitingtime > 1 ? 1 : waitingtime));
                            Task.Factory.StartNew(async () =>
                            {
                                await Task.Delay((int)((waitingtime > 1 ? 1 : waitingtime) * 1000));
                                this.MapX = mapX;
                                this.MapY = mapY;
                            });

                            for (int j = maxindex; j > 0; j--)
                            {
                                Path.RemoveAt(0);
                            }
                        }
                        if (Path.Count() == 0 && (targetSession == null || MapId != targetSession.Character.MapId || distance > maxDistance))
                        {
                            Path = ServerManager.GetMap(MapId).StraightPath(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = FirstX, Y = FirstY, MapId = this.MapId });
                            if (!Path.Any())
                            {
                                Path = ServerManager.GetMap(MapId).JPSPlus(new MapCell() { X = this.MapX, Y = this.MapY, MapId = this.MapId }, new MapCell() { X = FirstX, Y = FirstY, MapId = this.MapId });
                            }
                            Target = -1;
                        }
                    }
                }
            }
        }

        #endregion
    }
}