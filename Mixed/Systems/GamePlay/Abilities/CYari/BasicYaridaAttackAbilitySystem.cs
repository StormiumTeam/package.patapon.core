using System;
using package.stormiumteam.shared.ecs;
using Patapon.Mixed.GamePlay;
using Patapon.Mixed.GamePlay.Abilities.CYari;
using Patapon.Mixed.GamePlay.Physics;
using Patapon.Mixed.Units;
using StormiumTeam.GameBase;
using Unity.Entities;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;
using Random = Unity.Mathematics.Random;

namespace Systems.GamePlay.CYari
{
	public unsafe class BasicYaridaAttackAbilitySystem : BaseAbilitySystem
	{
		private SpearProjectile.Provider m_ProjectileProvider;

		protected override void OnCreate()
		{
			base.OnCreate();

			m_ProjectileProvider = World.GetOrCreateSystem<SpearProjectile.Provider>();
		}

		protected override JobHandle OnUpdate(JobHandle inputDeps)
		{
			var tick                   = ServerTick;
			var impl                   = new BasicUnitAbilityImplementation(this);
			var seekEnemies            = new SeekEnemies(this);
			var seekingStateFromEntity = GetComponentDataFromEntity<UnitEnemySeekingState>(true);

			var relativeTeamFromEntity = GetComponentDataFromEntity<Relative<TeamDescription>>(true);

			var queueWriter = m_ProjectileProvider.GetEntityDelayedStream()
			                                      .AsParallelWriter();

			var rand = new Random((uint) Environment.TickCount);
			Entities
				.ForEach((Entity entity, int nativeThreadIndex, ref RhythmAbilityState state, ref BasicYaridaAttackAbility ability, in Owner owner) =>
				{
					if (!impl.CanExecuteAbility(owner.Target))
						return;

					var tryGetChain = stackalloc[] {entity, owner.Target};
					if (!relativeTeamFromEntity.TryGetChain(tryGetChain, 2, out var relativeTeam))
						return;

					var seekingState = seekingStateFromEntity[owner.Target];
					var statistics   = impl.UnitSettingsFromEntity[owner.Target];
					var playState    = impl.UnitPlayStateFromEntity[owner.Target];
					var unitPosition = impl.TranslationFromEntity[owner.Target].Value;
					var direction    = impl.UnitDirectionFromEntity[owner.Target].Value;

					var velocityUpdater   = impl.VelocityFromEntity.GetUpdater(owner.Target).Out(out var velocity);
					var controllerUpdater = impl.ControllerFromEntity.GetUpdater(owner.Target).Out(out var controller);

					var attackStartTick = UTick.CopyDelta(tick, ability.AttackStartTick);

					if (state.IsStillChaining && !state.IsActive)
					{
						controller.ControlOverVelocity.x = true;
						velocity.Value.x                 = math.lerp(velocity.Value.x, 0, playState.GetAcceleration() * 50 * tick.Delta);
					}

					ability.NextAttackDelay -= tick.Delta;

					var throwOffset = new float3 {x = direction, y = 1.75f};
					var gravity     = new float3 {y = -22};
					if (ability.AttackStartTick > 0)
					{
						if (tick >= UTick.AddMs(attackStartTick, BasicYaridaAttackAbility.DelayThrowMs) && !ability.HasThrown)
						{
							var   damage      = playState.Attack;
							float damageFever = damage;

							var accuracy = 0.2f;
							if (state.Combo.IsFever)
							{
								accuracy += 0.3f;

								damageFever *= 1.2f;
								if (state.Combo.IsPerfect)
								{
									damageFever *= 1.2f;
									accuracy    += 0.3f;
								}

								damage += (int) damageFever - damage;
							}

							queueWriter.Enqueue(new SpearProjectile.Create
							{
								Owner       = owner.Target,
								Position    = unitPosition + throwOffset,
								Velocity    = {x = ability.ThrowSpeed * direction, y = ability.ThrowHeight + accuracy * rand.NextFloat()},
								StartDamage = damage,
								Gravity     = gravity
							});

							ability.HasThrown = true;
						}
						else if (!ability.HasThrown)
						{
							controller.ControlOverVelocity.x = true;
						}

						// stop moving
						if (ability.HasThrown)
							velocity.Value.x = math.lerp(velocity.Value.x, 0, playState.GetAcceleration() * 25 * tick.Delta);

						// stop attacking once the animation is done
						if (tick >= UTick.AddMs(attackStartTick, 500))
							ability.AttackStartTick = 0;
					}

					velocityUpdater.CompareAndUpdate(velocity);
					controllerUpdater.CompareAndUpdate(controller);

					if (!state.IsActive || seekingState.Enemy == default)
					{
						if (state.IsActive && state.Combo.IsFever)
						{
							playState.MovementReturnSpeed *= 1.2f;
							if (state.Combo.IsPerfect)
								playState.MovementReturnSpeed *= 1.4f;

							controller.ControlOverVelocity.x = true;
						}

						return;
					}

					if (state.Combo.IsFever)
					{
						playState.MovementAttackSpeed *= 1.2f;
						if (state.Combo.IsPerfect)
							playState.MovementAttackSpeed *= 1.4f;
					}

					var targetPosition     = impl.LocalToWorldFromEntity[seekingState.Enemy].Position;
					var throwDeltaPosition = PredictTrajectory.Simple(throwOffset, new float3 {x = ability.ThrowSpeed * direction, y = ability.ThrowHeight}, gravity);
					targetPosition.x -= throwDeltaPosition.x;

					var distanceMercy = 4f;
					if (math.abs(targetPosition.x - unitPosition.x) < distanceMercy && ability.NextAttackDelay <= 0 && ability.AttackStartTick <= 0)
					{
						var atkSpeed = playState.AttackSpeed;
						if (state.Combo.IsFever && state.Combo.IsPerfect)
							atkSpeed *= 0.75f;

						ability.NextAttackDelay = atkSpeed;
						ability.AttackStartTick = tick.AsUInt;
						ability.HasThrown       = false;

						var speed   = math.lerp(math.abs(velocity.Value.x), playState.MovementAttackSpeed, playState.GetAcceleration() * 5 * tick.Delta);
						var newPosX = Mathf.MoveTowards(unitPosition.x, targetPosition.x, speed * tick.Delta);

						var targetVelocityX = (newPosX - unitPosition.x) / tick.Delta;
						velocity.Value.x = targetVelocityX;
					}
					else if (tick >= UTick.AddMs(attackStartTick, BasicYaridaAttackAbility.DelayThrowMs))
					{
						var speed   = math.lerp(math.abs(velocity.Value.x), playState.MovementAttackSpeed, playState.GetAcceleration() * 20 * tick.Delta);
						var newPosX = Mathf.MoveTowards(unitPosition.x, targetPosition.x, speed * tick.Delta);

						var targetVelocityX = (newPosX - unitPosition.x) / tick.Delta;
						velocity.Value.x = targetVelocityX;
					}

					controller.ControlOverVelocity.x = true;

					velocityUpdater.CompareAndUpdate(velocity);
					controllerUpdater.CompareAndUpdate(controller);
				})
				.WithReadOnly(seekingStateFromEntity)
				.WithReadOnly(relativeTeamFromEntity)
				.Run();

			return default;
		}
	}
}