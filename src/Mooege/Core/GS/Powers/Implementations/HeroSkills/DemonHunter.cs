﻿/*
 * Copyright (C) 2011 mooege project
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
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mooege.Core.GS.Ticker;
using Mooege.Net.GS.Message.Definitions.Effect;
using Mooege.Core.GS.Common.Types.Math;
using Mooege.Core.GS.Powers.Payloads;
using Mooege.Core.GS.Common.Types.TagMap;
using Mooege.Core.GS.Actors;
using Mooege.Core.GS.Actors.Movement;
using Mooege.Net.GS.Message.Definitions.Actor;
using Mooege.Net.GS.Message;


namespace Mooege.Core.GS.Powers.Implementations
{
    //22 skills, 3 done by mdz, 2(vault and fan of knives) by velocityx, 8 started by wetwlly

    //TODO: Rune_E only right?
    #region BolaShot
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.BolaShot)]
    public class DemonHunterBolaShot : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            GeneratePrimaryResource(ScriptFormula(17));

            // fire projectile normally, or find targets in arc if RuneB
            Vector3D[] targetDirs;
            if (Rune_B > 0)
            {
                targetDirs = new Vector3D[(int)ScriptFormula(24)];

                int takenPos = 0;
                foreach (Actor actor in GetEnemiesInArcDirection(User.Position, TargetPosition, 75f, ScriptFormula(12)).Actors)
                {
                    targetDirs[takenPos] = actor.Position;
                    ++takenPos;
                    if (takenPos >= targetDirs.Length)
                        break;
                }

                // generate any extra positions using generic spread
                if (takenPos < targetDirs.Length)
                {
                    PowerMath.GenerateSpreadPositions(User.Position, TargetPosition, 10f, targetDirs.Length - takenPos)
                             .CopyTo(targetDirs, takenPos);
                }
            }
            else
            {
                targetDirs = new Vector3D[] { TargetPosition };
            }

            foreach (Vector3D position in targetDirs)
            {
                var proj = new Projectile(this, RuneSelect(77569, 153864, 153865, 153866, 153867, 153868), User.Position);
                proj.Position.Z += 5f;  // fix height
                proj.OnCollision = (hit) =>
                {
                    // hit effect
                    hit.PlayEffectGroup(RuneSelect(77577, 153870, 153872, 153873, 153871, 153869));

                    if (Rune_B > 0)
                        WeaponDamage(hit, ScriptFormula(9), DamageType.Poison);
                    else
                        AddBuff(hit, new ExplosionBuff());

                    proj.Destroy();
                };
                proj.Launch(position, ScriptFormula(2));

                if (Rune_B > 0)
                    yield return WaitSeconds(ScriptFormula(13));
            }
        }
        
        [ImplementsPowerBuff(0)]
        class ExplosionBuff : PowerBuff
        {
            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(4));
            }

            public override bool Update()
            {
                if (Timeout.TimedOut)
                {
                    Target.PlayEffectGroup(RuneSelect(77573, 153727, 154073, 154074, 154072, 154070));

                    if (Rune_D > 0)
                    {
                        if (Rand.NextDouble() < ScriptFormula(31))
                            GenerateSecondaryResource(ScriptFormula(32));
                    }

                    AttackPayload attack = new AttackPayload(this);
                    attack.Targets = GetEnemiesInRadius(Target.Position, ScriptFormula(20));
                    attack.AddWeaponDamage(ScriptFormula(6),
                        RuneSelect(DamageType.Fire, DamageType.Fire, DamageType.Poison,
                                   DamageType.Lightning, DamageType.Fire, DamageType.Arcane));
                    if (Rune_C > 0)
                    {
                        attack.OnHit = (hitPayload) =>
                        {
                            if (Rand.NextDouble() < ScriptFormula(28))
                                AddBuff(hitPayload.Target, new DebuffStunned(WaitSeconds(ScriptFormula(29))));
                        };
                    }
                    attack.Apply();
                }

                return base.Update();
            }

            public override bool Stack(Buff buff)
            {
                return false;
            }
        }
    }
    #endregion

    //Complete
    #region Grenades
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.Grenades)]
    public class DemonHunterGrenades : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            GeneratePrimaryResource(ScriptFormula(25));

            float targetDistance = PowerMath.Distance2D(User.Position, TargetPosition);
            
            // create grenade projectiles with shared detonation timer
            TickTimer timeout = WaitSeconds(ScriptFormula(2));
            Projectile[] grenades = new Projectile[Rune_C > 0 ? 1 : 3];
            for (int i = 0; i < grenades.Length; ++i)
            {
                var projectile = new Projectile(this, Rune_C > 0 ? 212547 : 88244, User.Position);
                projectile.Timeout = timeout;
                grenades[i] = projectile;
            }

            // generate spread positions with distance-scaled spread amount.
            float scaledSpreadOffset = Math.Max(targetDistance - ScriptFormula(14), 0f);
            Vector3D[] projDestinations = PowerMath.GenerateSpreadPositions(User.Position, TargetPosition,
                ScriptFormula(11) - scaledSpreadOffset, grenades.Length);

            // launch and bounce grenades
            yield return WaitTicks(1);  // helps make bounce timings more consistent

            float bounceOffset = 1f;
            float minHeight = ScriptFormula(21);
            float height = minHeight + ScriptFormula(22);
            float bouncePercent = 0.7f; // ScriptFormula(23);
            while (!timeout.TimedOut)
            {
                for (int i = 0; i < grenades.Length; ++i)
                {
                    grenades[i].LaunchArc(PowerMath.TranslateDirection2D(projDestinations[i], User.Position, projDestinations[i],
                                                                          targetDistance * 0.3f * bounceOffset),
                                          height, ScriptFormula(20));
                }

                height *= bouncePercent;
                bounceOffset *= 0.3f;

                yield return grenades[0].ArrivalTime;
                // play "dink dink" grenade bounce sound
                grenades[0].PlayEffect(Effect.Unknown69);
            }

            // damage effects
            foreach (var grenade in grenades)
            {
                var grenadeN = grenade;

                SpawnEffect(RuneSelect(154027, 154045, 154028, 154044, 154046, 154043), grenade.Position);

                // poison pool effect
                if (Rune_A > 0)
                {
                    var pool = SpawnEffect(154076, grenade.Position, 0, WaitSeconds(ScriptFormula(7)));
                    pool.UpdateDelay = 1f;
                    pool.OnUpdate = () =>
                    {
                        WeaponDamage(GetEnemiesInRadius(grenadeN.Position, ScriptFormula(5)), ScriptFormula(6), DamageType.Poison);
                    };
                }

                AttackPayload attack = new AttackPayload(this);
                attack.Targets = GetEnemiesInRadius(grenade.Position, ScriptFormula(4));
                attack.AddWeaponDamage(ScriptFormula(0), Rune_A > 0 ? DamageType.Poison : DamageType.Fire);
                attack.OnHit = (hitPayload) =>
                {
                    if (Rune_E > 0)
                    {
                        if (Rand.NextDouble() < ScriptFormula(9))
                            AddBuff(hitPayload.Target, new DebuffStunned(WaitSeconds(ScriptFormula(10))));
                    }
                    if (Rune_C > 0)
                        Knockback(grenadeN.Position, hitPayload.Target, ScriptFormula(8));
                };
                attack.Apply();
            }

            // clusterbomb hits
            if (Rune_B > 0)
            {
                int damagePulses = (int)ScriptFormula(28);
                for (int pulse = 0; pulse < damagePulses; ++pulse)
                {
                    yield return WaitSeconds(ScriptFormula(12) / damagePulses);

                    foreach (var grenade in grenades)
                    {
                        WeaponDamage(GetEnemiesInRadius(grenade.Position, ScriptFormula(4)), ScriptFormula(0), DamageType.Fire);
                    }
                }
            }
        }
    }
    #endregion

    //Complete
    #region RainOfVengeance
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.RainOfVengeance)]
    public class DemonHunterRainOfVengeance : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            //StartDefaultCooldown();
            UsePrimaryResource(EvalTag(PowerKeys.ResourceCost));

            // ground summon effect for rune c
            if (Rune_C > 0)
                SpawnProxy(TargetPosition).PlayEffectGroup(152294);

            // startup delay all version of skill have
            yield return WaitSeconds(ScriptFormula(3));

            IEnumerable<TickTimer> subScript;
            if (Rune_A > 0)
                subScript = _RuneA();
            else if (Rune_B > 0)
                subScript = _RuneB();
            else if (Rune_C > 0)
                subScript = _RuneC();
            else if (Rune_D > 0)
                subScript = _RuneD();
            else if (Rune_E > 0)
                subScript = _RuneE();
            else
                subScript = _NoRune();

            foreach (var timeout in subScript)
                yield return timeout;
        }

        IEnumerable<TickTimer> _RuneA()
        {
            Vector3D castedPosition = new Vector3D(User.Position);

            int demonCount = (int)ScriptFormula(23);
            for (int n = 0; n < demonCount; ++n)
            {
                var attackDelay = WaitSeconds(ScriptFormula(20));
                var demonPosition = RandomDirection(castedPosition, ScriptFormula(24), ScriptFormula(25));

                var demon = SpawnEffect(149949, demonPosition, ScriptFormula(22), WaitSeconds(5.0f));                    
                demon.OnUpdate = () =>
                {
                    if (attackDelay.TimedOut)
                    {
                        demon.PlayEffectGroup(152590);
                        WeaponDamage(GetEnemiesInRadius(demonPosition, ScriptFormula(27)), ScriptFormula(26), DamageType.Fire);

                        demon.OnUpdate = null;
                    }
                };

                yield return WaitSeconds(ScriptFormula(4));
            }
        }

        IEnumerable<TickTimer> _NoRune()
        {
            _CreateArrowPool(131701, new Vector3D(User.Position), ScriptFormula(6), ScriptFormula(7));
            yield break;
        }

        IEnumerable<TickTimer> _RuneB()
        {
            Vector3D castedPosition = new Vector3D(User.Position);

            TickTimer timeout = WaitSeconds(ScriptFormula(16));
            while (!timeout.TimedOut)
            {
                TargetList targets = GetEnemiesInRadius(castedPosition, ScriptFormula(18));
                if (targets.Actors.Count > 0)
                    _CreateArrowPool(153029, targets.Actors[Rand.Next(targets.Actors.Count)].Position, ScriptFormula(28), ScriptFormula(34));

                yield return WaitSeconds(ScriptFormula(38));
            }
        }

        void _CreateArrowPool(int actorSNO, Vector3D position, float duration, float radius)
        {
            var pool = SpawnEffect(actorSNO, position, 0, WaitSeconds(duration));
            pool.OnUpdate = () =>
            {
                TargetList targets = GetEnemiesInRadius(position, radius);
                targets.Actors.RemoveAll((actor) => Rand.NextDouble() > ScriptFormula(10));
                targets.ExtraActors.RemoveAll((actor) => Rand.NextDouble() > ScriptFormula(10));

                WeaponDamage(targets, ScriptFormula(0), DamageType.Physical);

                // rewrite delay every time for variation: base wait time * variation * user attack speed
                pool.UpdateDelay = (ScriptFormula(5) + (float)Rand.NextDouble() * ScriptFormula(2)) * (1.0f / ScriptFormula(9));
            };
        }

        IEnumerable<TickTimer> _RuneC()
        {
            var demon = new Projectile(this, 155276, TargetPosition);
            demon.Timeout = WaitSeconds(ScriptFormula(30));

            TickTimer grenadeTimer = null;
            demon.OnUpdate = () =>
            {
                if (grenadeTimer == null || grenadeTimer.TimedOut)
                {
                    grenadeTimer = WaitSeconds(ScriptFormula(31));

                    demon.PlayEffect(Effect.Sound, 215621);

                    var grenade = new Projectile(this, 152589, demon.Position);
                    grenade.Position.Z += 18f;  // make it spawn near demon's cannon
                    grenade.Timeout = WaitSeconds(ScriptFormula(33));
                    grenade.OnTimeout = () =>
                    {
                        grenade.PlayEffectGroup(154020);
                        WeaponDamage(GetEnemiesInRadius(grenade.Position, ScriptFormula(32)), ScriptFormula(0), DamageType.Fire);
                    };
                    grenade.LaunchArc(demon.Position, 0.1f, -0.1f, 0.6f);  // parameters not based on anything, just picked to look good
                }
            };

            bool firstLaunch = true;
            while (!demon.Timeout.TimedOut)
            {
                demon.Launch(RandomDirection(TargetPosition, 0f, ScriptFormula(7)), 0.2f);
                if (firstLaunch)
                {
                    demon.PlayEffectGroup(165237);
                    firstLaunch = false;
                }
                yield return demon.ArrivalTime;
            }
        }

        IEnumerable<TickTimer> _RuneD()
        {
            int flyerCount = (int)ScriptFormula(14);
            for (int n = 0; n < flyerCount; ++n)
            {
                var flyerPosition = RandomDirection(TargetPosition, 0f, ScriptFormula(7));
                var flyer = SpawnEffect(200808, flyerPosition, 0f, WaitSeconds(ScriptFormula(5)));
                flyer.OnTimeout = () =>
                {
                    flyer.PlayEffectGroup(200516);
                    AttackPayload attack = new AttackPayload(this);
                    attack.Targets = GetEnemiesInRadius(flyerPosition, ScriptFormula(13));
                    attack.AddWeaponDamage(ScriptFormula(12), DamageType.Fire);
                    attack.OnHit = (hitPayload) =>
                    {
                        AddBuff(hitPayload.Target, new DebuffStunned(WaitSeconds(ScriptFormula(37))));
                    };
                    attack.Apply();                    
                };

                yield return WaitSeconds(ScriptFormula(4));
            }
        }

        IEnumerable<TickTimer> _RuneE()
        {
            float attackRadius = 8f;  // value is not in formulas, just a guess
            Vector3D castedPosition = new Vector3D(User.Position);
            float castAngle = MovementHelpers.GetFacingAngle(castedPosition, TargetPosition);
            float waveOffset = 0f;

            int flyerCount = (int)ScriptFormula(15);
            for (int n = 0; n < flyerCount; ++n)
            {
                waveOffset += 3.0f;
                var wavePosition = PowerMath.TranslateDirection2D(castedPosition, TargetPosition, castedPosition, waveOffset);
                var flyerPosition = RandomDirection(wavePosition, 0f, attackRadius);
                var flyer = SpawnEffect(200561, flyerPosition, castAngle, WaitSeconds(ScriptFormula(20)));
                flyer.OnTimeout = () =>
                {
                    flyer.PlayEffectGroup(200819);
                    AttackPayload attack = new AttackPayload(this);
                    attack.Targets = GetEnemiesInRadius(flyerPosition, attackRadius);
                    attack.AddWeaponDamage(ScriptFormula(11), DamageType.Physical);
                    attack.OnHit = (hitPayload) => { Knockback(hitPayload.Target, 90f); };
                    attack.Apply();
                };

                yield return WaitSeconds(ScriptFormula(4));
            }
        }
    }
#endregion

    //TODO: needs the actor position fixed, since the arrow jumps from position to positions when changing direction
    #region HungeringArrow
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.HungeringArrow)]
    public class HungeringArrow : Skill
    {
        //BoneArrow
        public override IEnumerable<TickTimer> Main()
        {
            var projectile = new Projectile(this, RuneSelect(129932, 154590, 154591, 154592, 154593, 154594), User.Position);
            var target = GetEnemiesInRadius(TargetPosition, ScriptFormula(3)).GetClosestTo(TargetPosition);
            if (target != null)
            {
                projectile.Launch(target.Position, ScriptFormula(7));
                projectile.OnCollision = (hit) =>
                {
                    SpawnEffect(99572, new Vector3D(hit.Position.X, hit.Position.Y, hit.Position.Z + 5f)); // impact effect (fix height)
                    projectile.Destroy();
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Physical);
                };
            }
            else
            {
                projectile.Launch(TargetPosition, ScriptFormula(7));
                projectile.Position.Z += 5f;
                projectile.OnCollision = (hit) =>
                {
                    SpawnEffect(129934, new Vector3D(hit.Position.X, hit.Position.Y, hit.Position.Z + 5f));
                    projectile.Destroy();
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Physical);
                };

                for (int i = 0; i < 10; i++)
                {
                    var closetarget = GetEnemiesInRadius(projectile.Position, ScriptFormula(4)).GetClosestTo(projectile.Position);

                    if (closetarget != null)
                    {
                        var projectileSeek = new Projectile(this, RuneSelect(129932, 154590, 154591, 154592, 154593, 154594), projectile.Position);
                        projectile.Destroy();
                        projectileSeek.Launch(closetarget.Position, ScriptFormula(7));
                        projectileSeek.OnCollision = (hit) =>
                        {
                            SpawnEffect(129934, new Vector3D(hit.Position.X, hit.Position.Y, hit.Position.Z + 5f));
                            projectileSeek.Destroy();
                            WeaponDamage(hit, ScriptFormula(0), DamageType.Physical);
                        };
                        i = 10;
                    }
                    else
                        yield return WaitSeconds(0.1f);
                }
            }
            yield break;
        }
    }
    #endregion

    //TODO:Rune_E
    #region Impale
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.Impale)]
    public class Impale : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            if (Rune_D > 0)
            {
                //SpawnEffect(222156, User.Position);
                WeaponDamage(GetEnemiesInRadius(User.Position, ScriptFormula(11)), ScriptFormula(12), DamageType.Physical);
            }
            else
            {
                var proj = new Projectile(this, RuneSelect(220527, 222102, 222115, 222128, -1, 222141), User.Position);
                proj.Position.Z += 5f;  // fix height
                proj.OnCollision = (hit) =>
                {
                    hit.PlayEffectGroup(RuneSelect(221164, 222107, 222120, 222133, -1, 222146));
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Physical);

                    if (Rune_E > 0)
                    {
                        //if critical hit, do weapondamage (SF(13)) as well as damage below.
                    }

                    if (Rune_A > 0)
                    {
                        //Nothing goes here.
                    }
                    else
                    {
                        if (Rune_B > 0)
                        {
                            Knockback(User.Position, hit, ScriptFormula(4));
                            AddBuff(hit, new DebuffStunned(WaitSeconds(ScriptFormula(6))));
                        }
                        if (Rune_C > 0)
                        {
                            AddBuff(hit, new ActiveCalTrops());
                        }
                        proj.Destroy();

                    }
                };
                proj.Launch(TargetPosition, ScriptFormula(2));
            }
            yield return WaitSeconds(1f);
        }
        [ImplementsPowerBuff(0)]
        class ActiveCalTrops : PowerBuff
        {
            const float _damageRate = 1f;
            TickTimer _damageTimer = null;

            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(8));
            }

            public override bool Update()
            {
                if (base.Update())
                    return true;

                if (_damageTimer == null || _damageTimer.TimedOut)
                {
                    _damageTimer = WaitSeconds(_damageRate);

                    AttackPayload attack = new AttackPayload(this);
                    attack.AddWeaponDamage(ScriptFormula(9), DamageType.Physical);
                    attack.Apply();
                }

                return false;
            }
        }
    }
#endregion

    //TODO: BackFlip function and D and E <- these runes pretty much finish when backflip is corrected
    //TODO: Rune_C -> poison bomb.
    #region EvasiveFire
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.EvasiveFire)]
    public class EvasiveFire : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            GeneratePrimaryResource(ScriptFormula(4));

            // fire projectile normally, or find targets in arc if RuneB
            Vector3D[] targetDirs;
            if (Rune_B > 0)
            {
                targetDirs = new Vector3D[(int)ScriptFormula(24)];

                int takenPos = 0;
                foreach (Actor actor in GetEnemiesInArcDirection(User.Position, TargetPosition, ScriptFormula(25), ScriptFormula(26)).Actors)
                {
                    targetDirs[takenPos] = actor.Position;
                    ++takenPos;
                    if (takenPos >= targetDirs.Length)
                        break;
                }

                // generate any extra positions using generic spread
                if (takenPos < targetDirs.Length)
                {
                    PowerMath.GenerateSpreadPositions(User.Position, TargetPosition, 10f, targetDirs.Length - takenPos)
                             .CopyTo(targetDirs, takenPos);
                }
            }
            else
            {
                targetDirs = new Vector3D[] { TargetPosition };
            }

            foreach (Vector3D position in targetDirs)
            {
                User.PlayEffectGroup(134689);
                var proj = new Projectile(this, 178987, User.Position);
                proj.Position.Z += 5f;  // fix height
                proj.OnCollision = (hit) =>
                {
                    // hit effect
                    if (Rune_A > 0)
                    {
                        hit.PlayEffectGroup(147971);
                        WeaponDamage(GetEnemiesInRadius(hit.Position, ScriptFormula(21)), ScriptFormula(20), DamageType.Fire);
                    }
                    else
                    {
                        hit.PlayEffectGroup(RuneSelect(134836, 150801, 150807, 150803, 150804, 150805));
                        WeaponDamage(hit, ScriptFormula(3), RuneSelect(DamageType.Physical,DamageType.Fire,DamageType.Physical,DamageType.Poison, DamageType.Lightning, DamageType.Physical));
                    }
                        
                    proj.Destroy();
                };
                proj.Launch(position, ScriptFormula(2));

                /*if (GetEnemiesInArcDirection(User.Position, TargetPosition, ScriptFormula(5), ScriptFormula(6)).Actors.Count > 0)
                {
                 //if Rune C -> lay poison bomb behind.
                 //if rune D -> UseSecondaryResource(ScriptFormula(8))
                    float speed = Target.Attributes[GameAttribute.Running_Rate_Total] * 3f;
                    Vector3D destination = //this needs to be the opposite direction of the facing direction// ScriptFormula(7);
                    ActorMover _mover;
                    //lets move backwards!
                    User.TranslateFacing(TargetPosition, true);
                    _mover = new ActorMover(Target);
                    _mover.Move(destination, speed, new NotifyActorMovementMessage
                    {
                        AnimationTag = 69824, // dashing strike attack animation
                    });
                    //backflip
                } */

                yield break;
            }
        }
    }
    #endregion

    //TODO:Rune_C and E 
    #region Caltrops
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.Caltrops)]
    public class Caltrops : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            UseSecondaryResource(ScriptFormula(7));

            var GroundSpot = SpawnProxy(User.Position);
            var caltropsGround = SpawnEffect(196030, GroundSpot.Position, 0, WaitSeconds(ScriptFormula(9)));
            caltropsGround.UpdateDelay = 0.25f;
            caltropsGround.OnUpdate = () =>
                {
                    if (GetEnemiesInRadius(GroundSpot.Position, ScriptFormula(0)).Actors.Count > 0)
                    {
                        caltropsGround.Destroy();
                        var calTrops = SpawnEffect(RuneSelect(129784, 154811, 155734, 155159, 155848, 155376), GroundSpot.Position, 0, WaitSeconds(ScriptFormula(2)));
                        AttackPayload attack = new AttackPayload(this);
                        attack.Targets = GetEnemiesInRadius(GroundSpot.Position, ScriptFormula(0));
                        attack.OnHit = (hit) =>
                            {
                                if (AddBuff(hit.Target, new ActiveCalTrops()))
                                { }
                                else
                                AddBuff(hit.Target, new ActiveCalTrops());
                            };
                        attack.Apply();

                        if (Rune_E > 0)
                        {
                            //Increase Crit Hit while inside Radius. May have to redo skill for this?
                        }
                    }
                };

            yield break;
        }

        [ImplementsPowerBuff(0)]
        class ActiveCalTrops : PowerBuff
        {
            const float _damageRate = 1f;
            TickTimer _damageTimer = null;

            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(2));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;

                return true;
            }
            public override bool Update()
            {
                if (base.Update())
                    return true;

                if (_damageTimer == null || _damageTimer.TimedOut)
                {
                    _damageTimer = WaitSeconds(_damageRate);

                    AttackPayload attack = new AttackPayload(this);
                    attack.Targets = GetEnemiesInRadius(Target.Position, ScriptFormula(0));
                    if (Rune_A > 0)
                    {
                        attack.AddWeaponDamage(ScriptFormula(20), DamageType.Physical);
                    }
                    attack.OnHit = HitPayload =>
                        {
                            if (Rune_C > 0)
                            {
                                //Immobilize for ScriptFormula(24)
                            }

                            if (AddBuff(HitPayload.Target, new DebuffSlowed(ScriptFormula(3), WaitSeconds(ScriptFormula(4)))))
                            { }
                            else
                            AddBuff(HitPayload.Target, new DebuffSlowed(ScriptFormula(3), WaitSeconds(ScriptFormula(4))));
                        };
                    attack.Apply();
                }

                return false;
            }

            public override bool Stack(Buff buff)
            {
                return false;
            }
        }
    }
    #endregion

    //Partially Complete, No runes.
    #region RapidFire
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.RapidFire)]
    public class RapidFire : ChanneledSkill
    {
        private Actor _target = null;

        public override void OnChannelOpen()
        {
            EffectsPerSecond = 0.1f;
            //User.PlayEffectGroup(150049); //unknown where this could go.
            User.Attributes[GameAttribute.Projectile_Speed] = User.Attributes[GameAttribute.Projectile_Speed] * ScriptFormula(22);
            User.Attributes.BroadcastChangedIfRevealed();
        }

        public override void OnChannelClose()
        {
            if (_target != null)
                _target.Destroy();
            User.Attributes[GameAttribute.Projectile_Speed] = User.Attributes[GameAttribute.Projectile_Speed] / ScriptFormula(22);
            User.Attributes.BroadcastChangedIfRevealed();
        }

        public override void OnChannelUpdated()
        {
            User.TranslateFacing(TargetPosition);
            // client updates target actor position
        }

        public override IEnumerable<TickTimer> Main()
        {
            //initial hatred cost (4)
            UsePrimaryResource(1.5f);
            //projectiles
            var proj1 = new Projectile(this, 150061, User.Position);
            proj1.Position.Z += 5f;
            proj1.Launch(TargetPosition, ScriptFormula(2));
            proj1.OnCollision = (hit) =>
            {
                SpawnEffect(99572, new Vector3D(hit.Position.X, hit.Position.Y, hit.Position.Z + 5f)); // impact effect (fix height)
                proj1.Destroy();
                WeaponDamage(hit, ScriptFormula(0), DamageType.Arcane);
            };

            yield return WaitSeconds(ScriptFormula(1));
        }
    }
    #endregion

    //Started, no entangling or runes yet
    #region EntanglingShot
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.EntanglingShot)]
    public class EntanglingShot : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            var proj1 = new Projectile(this, 75678, User.Position);
            proj1.Position.Z += 5f;
            proj1.OnCollision = (hit) =>
            {
                //TODO: Rope effect to mobs
                hit.PlayEffectGroup(76228); // impact effect (fix height)
                proj1.Destroy();
                WeaponDamage(hit, ScriptFormula(5), DamageType.Physical);
                AddBuff(hit, new EntangleDebuff());
            };
            proj1.Launch(TargetPosition, ScriptFormula(12));

            yield break;
        }
        [ImplementsPowerBuff(0)]
        class EntangleDebuff : PowerBuff
        {
            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(4));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;

                Target.Attributes[GameAttribute.Movement_Scalar_Reduction_Percent] += ScriptFormula(0);
                Target.Attributes.BroadcastChangedIfRevealed();

                return true;
            }

            public override void Remove()
            {
                base.Remove(); 
                Target.Attributes[GameAttribute.Movement_Scalar_Reduction_Percent] -= ScriptFormula(0);
                Target.Attributes.BroadcastChangedIfRevealed();
            }
        }
    }
    #endregion

    //Rune_A,D,E
    #region ElementalArrow
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.ElementalArrow)]
    public class ElementalArrow : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            
            var proj = new Projectile(this, RuneSelect(77604, 131664, 155092, 155749, 155938, 154674), User.Position);
            if (Rune_C > 0)
            { proj.Position.Z += 3f; }
            else
            { proj.Position.Z += 5f; }
            proj.OnCollision = (hit) =>
            {
                hit.PlayEffectGroup(RuneSelect(154844, 155087, 154845, -1, 156007, 154846));
                if (Rune_A > 0)
                {
                    //cone damage in arch from user to target, from target's position "Thru its back"
                    TargetPosition = PowerMath.TranslateDirection2D(User.Position, hit.Position,
                                                             new Vector3D(User.Position.X, User.Position.Y, TargetPosition.Z),
                                                             35f);
                    //var FirstTarget = GetEnemiesInArcDirection(hit.Position, TargetPosition, ScriptFormula(13), ScriptFormula(14)).SortByDistanceFrom(User.Position);
                    //WeaponDamage(FirstTarget, ScriptFormula(0), DamageType.Cold);
                    //FirstTarget.PlayEffectGroup(131673);
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Cold);
                }
                if (Rune_B > 0)
                {
                    WeaponDamage(GetEnemiesInRadius(hit.Position, ScriptFormula(22)), ScriptFormula(0), DamageType.Lightning);
                }
                if (Rune_C > 0)
                {
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Physical);
                
                    if (Rand.NextDouble() < ScriptFormula(5))
                    {
                        AddBuff(hit, new FearedDebuff());
                        AddBuff(hit, new DebuffFeared(WaitSeconds(ScriptFormula(6))));
                    }
                }
                if (Rune_D > 0)
                {
                    //radius damage to mobs, and heal user for 24% of damage
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Poison);
                }
                if (Rune_E > 0)
                {
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Lightning);
                    //when critical hit, stun enemies -> AddBuff(hit, new DebuffStunned(WaitSeconds(ScriptFormula(38))));
                }
                else
                    WeaponDamage(hit, ScriptFormula(0), DamageType.Fire);
                
            };
            if (Rune_B > 0 || Rune_D > 0)
            { proj.Launch(TargetPosition, ScriptFormula(20)); }
            else
            proj.Launch(TargetPosition, ScriptFormula(1));

            yield break;
        }
        [ImplementsPowerBuff(0)]
        class FearedDebuff : PowerBuff
        {
            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(6));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;

                return true;
            }

            public override void Remove()
            {
                base.Remove();
            }
        }
    }
    #endregion

    //Complete
    #region ShadowPower
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.ShadowPower)]
    public class ShadowPower : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            UseSecondaryResource(EvalTag(PowerKeys.ResourceCost));
            //if Female
            AddBuff(User, new ShadowPowerFemale());
            //else
            //AddBuff(User, new ShadowPower());

            yield break;
        }
        [ImplementsPowerBuff(0)]
        class ShadowPowerFemale : PowerBuff
        {
            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(0));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;
                Target.Attributes[GameAttribute.Attacks_Per_Second_Percent] += ScriptFormula(0);

                if (Rune_A > 0)
                {
                    Target.Attributes[GameAttribute.Movement_Bonus_Run_Speed] += ScriptFormula(2);
                } 
                if (Rune_B > 0)
                {
                    Target.Attributes[GameAttribute.Resource_Regen_Bonus_Percent] += ScriptFormula(3);
                }
                if (Rune_C > 0)
                {
                    Target.Attributes[GameAttribute.Dodge_Chance_Bonus] += ScriptFormula(4);
                }
                if (Rune_E > 0)
                {
                    Target.Attributes[GameAttribute.Damage_Percent_Reduction_Turns_Into_Heal] += ScriptFormula(5);
                }

                Target.Attributes.BroadcastChangedIfRevealed();
                return true;
            }

            public override void Remove()
            {
                base.Remove();
                Target.Attributes[GameAttribute.Attacks_Per_Second_Percent] -= ScriptFormula(0);

                if (Rune_A > 0)
                {
                    Target.Attributes[GameAttribute.Movement_Bonus_Run_Speed] -= ScriptFormula(2);
                }
                if (Rune_B > 0)
                {
                    Target.Attributes[GameAttribute.Resource_Regen_Bonus_Percent] -= ScriptFormula(3);
                }
                if (Rune_C > 0)
                {
                    Target.Attributes[GameAttribute.Dodge_Chance_Bonus] -= ScriptFormula(4);
                }
                if (Rune_E > 0)
                {
                    Target.Attributes[GameAttribute.Damage_Percent_Reduction_Turns_Into_Heal] -= ScriptFormula(5);
                }

                Target.Attributes.BroadcastChangedIfRevealed();
            }
        }
    }
    #endregion

    //TODO: Max Traps + Runes_B,C,D,E Once maxtraps gets figured out, B will be solved.
    #region SpikeTrap
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.SpikeTrap)]
    public class SpikeTrap : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            UseSecondaryResource(ScriptFormula(7));

            if (Rune_C > 0)
            {
                TickTimer timeout = WaitSeconds(ScriptFormula(4));
                //TODO:there needs to be a traget for this. and needs to be its own function because you're planting it on a target.
                if (timeout.TimedOut)
                {
                    WeaponDamage(GetEnemiesInRadius(Target.Position, ScriptFormula(6)), ScriptFormula(0), DamageType.Fire);
                }
            }
            else
            {
                var GroundSpot = SpawnProxy(User.Position);
                var caltropsGround = SpawnEffect(111330, GroundSpot.Position, 0, WaitSeconds(ScriptFormula(4)));

                yield return WaitSeconds(ScriptFormula(3));

                caltropsGround.UpdateDelay = 0.25f;
                caltropsGround.OnUpdate = () =>
                {
                    if (GetEnemiesInRadius(GroundSpot.Position, ScriptFormula(5)).Actors.Count > 0)
                    {
                        caltropsGround.Destroy();
                        var calTrops = SpawnEffect(75887, GroundSpot.Position);
                        AttackPayload attack = new AttackPayload(this);
                        attack.Targets = GetEnemiesInRadius(GroundSpot.Position, ScriptFormula(5));
                        attack.AddWeaponDamage(ScriptFormula(0), DamageType.Physical);
                        attack.Apply();

                    }
                };
            }

            yield break;
        }
    }
    #endregion

    //Complete, look over Rune_e's effect..
    #region Multishot
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.Multishot)]
    public class Multishot : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            UsePrimaryResource(ScriptFormula(14));

            User.PlayEffectGroup(RuneSelect(77647, 154203, 154204, 154208, 154211, 154212));
            AttackPayload attack = new AttackPayload(this);
            attack.Targets = GetEnemiesInArcDirection(User.Position, TargetPosition, ScriptFormula(21), ScriptFormula(23));
            attack.AddWeaponDamage(ScriptFormula(0), 
                RuneSelect(DamageType.Physical, DamageType.Physical, DamageType.Physical, DamageType.Physical, 
                DamageType.Lightning, DamageType.Physical));
            if (Rune_E > 0)
            {
                attack.OnHit = HitPayload =>
                {
                    //Every enemy hit grants 1 Discipline. Each volley can gain up to SF(10) Discipline in this way.
                    GenerateSecondaryResource(Math.Min(ScriptFormula(9), ScriptFormula(10)));
                };
            }
            attack.Apply();

            if (Rune_B > 0)
            {
                User.PlayEffectGroup(154409);
                WeaponDamage(GetEnemiesInRadius(User.Position, ScriptFormula(2)), ScriptFormula(1), DamageType.Arcane);
            }
            yield return WaitSeconds(ScriptFormula(17));

            if (Rune_C > 0)
            {
                Vector3D[] targetDirs;
                targetDirs = new Vector3D[(int)ScriptFormula(3)];

                int takenPos = 0;
                foreach (Actor actor in GetEnemiesInArcDirection(User.Position, TargetPosition, ScriptFormula(4), ScriptFormula(23)).Actors)
                {
                    targetDirs[takenPos] = actor.Position;
                    ++takenPos;
                    if (takenPos >= targetDirs.Length)
                        break;
                }

                if (takenPos < targetDirs.Length)
                {
                    PowerMath.GenerateSpreadPositions(User.Position, TargetPosition, 10f, targetDirs.Length - takenPos)
                             .CopyTo(targetDirs, takenPos);
                }

                foreach (Vector3D position in targetDirs)
                {
                    var proj = new Projectile(this, 154939, User.Position);
                    proj.Position.Z += 5f;  // fix height
                    proj.OnCollision = (hit) =>
                    {
                        // hit effect
                        hit.PlayEffectGroup(196636);

                        if (Rune_B > 0)
                            WeaponDamage(hit, ScriptFormula(6), DamageType.Fire);

                        proj.Destroy();
                    };
                    proj.Launch(position, ScriptFormula(20));
                }
            }

            yield break;
        }
    }
    #endregion

    //TODO:Rune_A -> cloud isnt working for some reason..
    #region SmokeScreen
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.SmokeScreen)]
    public class SmokeScreen : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            StartDefaultCooldown();
            UseSecondaryResource(ScriptFormula(2));
            
            AddBuff(User, new SmokeScreenBuff());
                
            //AddBuff(GroundArea, new SmokeScreenCloud());

            yield break;
        }
        [ImplementsPowerBuff(0)]
        class SmokeScreenCloud : PowerBuff
        {
            const float _damageRate = 1f;
            TickTimer _damageTimer = null;

            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(4));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;
                return true;
            }
            public override bool Update()
            {
                if (base.Update())
                    return true;

                if (_damageTimer == null || _damageTimer.TimedOut)
                {
                    _damageTimer = WaitSeconds(_damageRate);
                    if (GetEnemiesInRadius(Target.Position, ScriptFormula(3)) != null)
                    {
                        AttackPayload attack = new AttackPayload(this);
                        attack.Targets = GetEnemiesInRadius(Target.Position, ScriptFormula(3));
                        attack.AddWeaponDamage(ScriptFormula(5), DamageType.Physical);
                        attack.Apply();
                    }
                }

                return false;
            }
            public override void Remove()
            {
                base.Remove();
            }
        }
        [ImplementsPowerBuff(2)]
        class SmokeScreenBuff : PowerBuff
        {
            const float _damageRate = 1f;
            TickTimer _damageTimer = null;

            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(0));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;
                User.Attributes[GameAttribute.Look_Override] = 0x04E733FD;
                User.Attributes[GameAttribute.Stealthed] = true;
                
                if (Rune_E > 0)
                {
                    User.Attributes[GameAttribute.Movement_Bonus_Run_Speed] += ScriptFormula(12);
                }
                return true;
            }

            public override bool Update()
            {
                if (base.Update())
                    return true;
                if (Rune_C > 0)
                {
                    if (_damageTimer == null || _damageTimer.TimedOut)
                    {
                        _damageTimer = WaitSeconds(_damageRate);

                        GeneratePrimaryResource(ScriptFormula(10));
                    }
                }

                return false;
            }

            public override void Remove()
            {
                base.Remove();
                User.Attributes[GameAttribute.Stealthed] = false;
                User.Attributes[GameAttribute.Look_Override] = 0;

                if (Rune_E > 0)
                {
                    User.Attributes[GameAttribute.Movement_Bonus_Run_Speed] -= ScriptFormula(12);
                }
            }
        }
    }
    #endregion

    //TODO: possibly redo, this is a concept.
    #region Strafe
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.Strafe)]
    public class Strafe : ChanneledSkill
    {
        private Actor _target = null;

        public override void OnChannelOpen()
        {
            EffectsPerSecond = 0.25f;
            //User.PlayEffectGroup(150049); //unknown where this could go.
            User.Attributes[GameAttribute.Projectile_Speed] = User.Attributes[GameAttribute.Projectile_Speed] * ScriptFormula(13);
            User.Attributes[GameAttribute.Movement_Bonus_Run_Speed] += ScriptFormula(8);
            User.Attributes.BroadcastChangedIfRevealed();
        }

        public override void OnChannelClose()
        {
            if (_target != null)
                _target.Destroy();
            User.Attributes[GameAttribute.Projectile_Speed] = User.Attributes[GameAttribute.Projectile_Speed] / ScriptFormula(13);
            User.Attributes[GameAttribute.Movement_Bonus_Run_Speed] -= ScriptFormula(8);
            User.Attributes.BroadcastChangedIfRevealed();
        }

        public override void OnChannelUpdated()
        {
            User.TranslateFacing(TargetPosition);
            // client updates target actor position
        }

        public override IEnumerable<TickTimer> Main()
        {
            //"Use SpecialWalk Steering"

            UsePrimaryResource(ScriptFormula(19));
            //projectiles
            var Target = GetEnemiesInRadius(User.Position, ScriptFormula(2)).GetClosestTo(User.Position);
            //todo:else should it fire if there are no mobs? seems like it should but unknown how that should work.  

            var proj1 = new Projectile(this, 149790, User.Position);
            proj1.Position.Z += 6f;
            proj1.Launch(Target.Position, ScriptFormula(10));
            proj1.OnCollision = (hit) =>
            {
                SpawnEffect(218504, new Vector3D(hit.Position.X, hit.Position.Y, hit.Position.Z + 6f)); // impact effect (fix height)
                proj1.Destroy();
                WeaponDamage(hit, ScriptFormula(0), DamageType.Physical);
            };

            yield return WaitSeconds(ScriptFormula(1));
        }
    }
    #endregion

    //Main skill complete, TODO: Runes.
    #region MarkedForDeath
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.MarkedForDeath)]
    public class MarkedForDeath : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            UseSecondaryResource(EvalTag(PowerKeys.ResourceCost));
            //StartDefaultCooldown();

                
                AddBuff(Target, new DeathMarkBuff());


            yield break;
        }
        [ImplementsPowerBuff(0)]
        class DeathMarkBuff : PowerBuff
        {
            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(0));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;
                Target.Attributes[GameAttribute.Defense_Reduction_Percent] += ScriptFormula(1);
                return true;
            }
            public override void Remove()
            {
                base.Remove();
                Target.Attributes[GameAttribute.Defense_Reduction_Percent] -= ScriptFormula(1);
            }
        }
    }
    #endregion

    //Complete
    #region Preparation
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.Preparation)]
    public class Preparation : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            User.PlayEffectGroup(RuneSelect(132466, 148872, 148873, 148874, 148875, 148876));
            if (Rune_A > 0)
            {
                UseSecondaryResource(ScriptFormula(0));
                GeneratePrimaryResource(999f);
            }
            else if (Rune_D > 0)
            {
                StartCooldown(WaitSeconds(120f));
                GenerateSecondaryResource(999f);
                //Restore 55% of life
            }
            else if (Rune_E > 0)
            {
                if (Rand.NextDouble() < ScriptFormula(7))
                {
                    User.PlayEffectGroup(158497);
                    GenerateSecondaryResource(999f);
                }
                else
                    StartCooldown(WaitSeconds(120f));
                    GenerateSecondaryResource(999f);
            }
            else
            {
                StartCooldown(WaitSeconds(120f));
                GenerateSecondaryResource(999f);
                if (Rune_B > 0)
                {
                    AddBuff(User, new IndigoBuff());
                }
                if (Rune_C > 0)
                {
                    AddBuff(User, new ObsidianBuff());
                }
            }
            yield break;
        }
        [ImplementsPowerBuff(0)]
        class IndigoBuff : PowerBuff
        {
            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(2));
            }
            public override bool Apply()
            {
                if (!base.Apply())
                    return false;
                //is there no bonus for discipline?
                Target.Attributes[GameAttribute.Resource_Max_Bonus] += ScriptFormula(1);
                return true;
            }
            public override void Remove()
            {
                base.Remove();
                Target.Attributes[GameAttribute.Resource_Max_Bonus] -= ScriptFormula(1);
            }
        }
        [ImplementsPowerBuff(1)]
        class ObsidianBuff : PowerBuff
        {
            const float _damageRate = 1f;
            TickTimer _damageTimer = null;

            public override void Init()
            {
                base.Init();
                Timeout = WaitSeconds(ScriptFormula(5));
            }

            public override bool Update()
            {
                if (base.Update())
                    return true;

                if (_damageTimer == null || _damageTimer.TimedOut)
                {
                    _damageTimer = WaitSeconds(_damageRate);

                    GenerateSecondaryResource(ScriptFormula(4) / ScriptFormula(5));
                }

                return false;
            }
        }
    }
    #endregion

    //TODO: project and main explosion work, need baby grenades bouncing fixed and then baby explosion should be okay.
    #region ClusterArrow
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.ClusterArrow)]
    public class ClusterArrow : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            var GroundSpot = SpawnProxy(TargetPosition);

            var proj = new Projectile(this, RuneSelect(129603, 166549, 166550, 167218, 166636, 166621), User.Position);
            proj.Position.Z += 5f;  // fix height
            proj.Launch(GroundSpot.Position, ScriptFormula(3));
            proj.OnArrival = () =>
            {
                //main explosion
                proj.Destroy();
                var Impact = SpawnEffect(129787, GroundSpot.Position);
                WeaponDamage(GetEnemiesInRadius(GroundSpot.Position, ScriptFormula(1)), ScriptFormula(0), DamageType.Fire);
            };



            /*TickTimer timeout = WaitSeconds(2f);
            Projectile[] grenades = new Projectile[4];
            for (int i = 0; i < grenades.Length; ++i)
            {
                var projectile = new Projectile(this, 129621, GroundSpot.Position);
                projectile.Timeout = timeout;
                grenades[i] = projectile;
            }

            Vector3D[] projDestinations = PowerMath.GenerateSpreadPositions(GroundSpot.Position, RandomDirection(GroundSpot.Position, 5f), 90f, grenades.Length);
            // launch and bounce grenades
            yield return WaitTicks(1);  // helps make bounce timings more consistent

            float bounceOffset = 5f;
            float minHeight = ScriptFormula(24);
            float height = minHeight + ScriptFormula(25);
            float bouncePercent = 0.3f; // ScriptFormula(23);
            while (!timeout.TimedOut)
            {
                for (int i = 0; i < grenades.Length; ++i)
                {
                    grenades[i].LaunchArc(PowerMath.TranslateDirection2D(projDestinations[i], GroundSpot.Position, projDestinations[i], 5f - 0.3f * bounceOffset), height, ScriptFormula(32), ScriptFormula(34));
                }

                height *= bouncePercent;
                bounceOffset *= 0.3f;

                yield return grenades[0].ArrivalTime;
            }
            foreach (var grenade in grenades)
            {
                var grenadeN = grenade;

                SpawnEffect(129788, grenade.Position);

                AttackPayload attack = new AttackPayload(this);
                attack.Targets = GetEnemiesInRadius(grenade.Position, ScriptFormula(6));
                attack.AddWeaponDamage(ScriptFormula(5), DamageType.Fire);
                attack.Apply();
            }*/
            yield break;
        }
    }
    #endregion

    //Not Started. Attempted and failed.
    #region Chakram
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.Chakram)]
    public class Chakram : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            //http://www.youtube.com/watch?v=9xKCTla3sQU

            //swirling motion with projectile is NoRune.

            //Rune_A has two chakrams, both same direction just flipped paths

            //Rune_B makes a loop around then destroys

            //Rune_C makes a slow curve

            //Rune_D spirals out to target, actor calls it a straight projectile.

            //Rune_E is just a buff shield
            yield break;
        }
    }
    #endregion

    //pet class
    #region Sentry
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.Sentry)]
    public class Sentry : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            yield break;
        }
    }
    #endregion    
    
    //TODO:Pet class.
    #region Companion
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.Discipline.Companion)]
    public class Companion : Skill
    {

        public override IEnumerable<TickTimer> Main()
        {
            yield break;
        }
    }
    #endregion

    //TODO: Add Fan of knives and Vault -> Need Velocityx or ill do it later.
    //spirit walk: 0xF2F224EA  (used on pet proxy)
    //vault: 0x04E733FD 
    //diamondskin: 0x061F7489
    //smokescreen: 0x04E733FD

    //12 Passive Skills
}
