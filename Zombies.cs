// Reference: RustLib
using Facepunch;
using Rust;
using RustLib.AI;
using RustLib.Diagnostics;
using RustLib.Extensions;
using RustLib.FileSystem;
using RustLib.Math3D;
using RustLib.Misc;
using RustLib.Threading;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using UnityEngine;

namespace Oxide.Plugins
{
	[Info("Zombies", "Sulley", 1.0)]
	[Description("Just can't explain it.")]
	public class Zombies : RustPlugin
	{
		private static float perlin_muliplier = 500.0f;

		public class BotConfiguration
		{
			public bool should_tick = true;
			public float wander_target_swapover_distance;
			public float player_detect_distance;
			public float walk_speed_s = 2.838214f;
			public float run_speed_s = 5.504924f;
			public float attack_distance;
			public float stop_pursuit_distance;
			public float damage;
			public Func<GameManager, PathfindingOptions> create_pathfinding_options;
			public Func<GameManager, BotController, BasePlayer> check_for_nearby_players;
			public Action<GameManager, BotController, BasePlayer> on_detect_player;
			public Action<GameManager, BotController> on_target_player_death;
			public string name;
			public List<int> weapons = new List<int>();
			public List<int> attire = new List<int>();

			public static BotConfiguration MeleePursuer()
			{
				BotConfiguration configuration = new BotConfiguration();

				configuration.wander_target_swapover_distance = 5.0f;
				configuration.player_detect_distance = 15.0f;
				configuration.attack_distance = 2.5f;
				configuration.stop_pursuit_distance = 2.0f;
				configuration.damage = 10.0f;

				configuration.create_pathfinding_options = (GameManager game) =>
				{
					PathfindingOptions pathfinding_options = PathfindingOptions.RandomPerlin();

					pathfinding_options.perlin_multiplier = perlin_muliplier;
					pathfinding_options.perlin_coord_multiplier = 0.1f;
					pathfinding_options.perlin_x_offset = (float)game.random.Between(-1000.0f, 1000.0f);
					pathfinding_options.perlin_y_offset = (float)game.random.Between(-1000.0f, 1000.0f);

					return pathfinding_options;
				};

				configuration.check_for_nearby_players = (GameManager game, BotController controller) =>
				{
					foreach (BasePlayer player in BasePlayer.activePlayerList)
					{
						float distance = Vector3.Distance(player.transform.position, controller.bot_player.transform.position);

						if (distance < configuration.player_detect_distance && player.IsAlive())
							return player;
					}

					return null;
				};

				configuration.on_detect_player = (GameManager game, BotController controller, BasePlayer player) =>
				{
					if (game.last_recorded_mesh_positions.ContainsKey(player))
						controller.SetNextState(new BotPursuitState(player));
				};

				configuration.on_target_player_death = (GameManager game, BotController controller) =>
				{
					controller.SetNextState(new BotWanderState());
				};

				configuration.weapons.Add(-1368584029); // sickle
				configuration.attire.Add(273951840); // scarecrow suit
				configuration.attire.Add(-690276911); // glowing eyes

				configuration.name = "Sickle Boi";

				return configuration;
			}

			public static BotConfiguration DoubleBarrel()
			{
				BotConfiguration configuration = MeleePursuer();

				configuration.attack_distance = 10.0f;
				configuration.stop_pursuit_distance = 5.0f;
				configuration.damage = 10.0f;

				configuration.weapons.Clear();

				configuration.weapons.Add(-765183617); // double barrel

				configuration.name = "Double Barrel Boi";

				return configuration;
			}

			public static BotConfiguration FFA()
			{
				BotConfiguration configuration = MeleePursuer();

				configuration.check_for_nearby_players = (GameManager game, BotController controller) =>
				{
					List<BasePlayer> player_list = Pool.GetList<BasePlayer>();

					foreach (BasePlayer key in game.bot_controllers.Keys)
						player_list.Add(key);

					float closest_distance = Single.MaxValue;
					BasePlayer closest_player = null;

					foreach (BasePlayer player in player_list)
					{
						if (!player.IsAlive()) continue;

						float distance = Vector3.Distance(controller.bot_player.transform.position, player.transform.position);
						bool player_is_not_me = (player != controller.bot_player);

						if (distance < closest_distance && player_is_not_me)
						{
							closest_distance = distance;
							closest_player = player;
						}
					}

					Pool.FreeList<BasePlayer>(ref player_list);

					if (closest_distance < controller.configuration.player_detect_distance)
						return closest_player;
					else
						return null;
				};

				Action<GameManager, BotController, BasePlayer> on_detect_player = configuration.on_detect_player;
				Action<GameManager, BotController> on_target_death = configuration.on_target_player_death;

				configuration.on_detect_player = (GameManager game, BotController controller, BasePlayer player) =>
				{
					game.debug("on detect player");
					on_detect_player(game, controller, player);
				};

				configuration.on_target_player_death = (GameManager game, BotController controller) =>
				{
					game.debug("target player death");
					on_target_death(game, controller);
				};

				return configuration;
			}

			public static BotConfiguration BoxBoi()
			{
				BotConfiguration configuration = MeleePursuer();
				configuration.attire.Add(1189981699); // it's just a box
				return configuration;
			}

			public BotController CreateBot(GameManager game, Vector3 position, Vector3 look)
			{
				BasePlayer bot_player = CreateZombie(game, position, look, name);
				BotController controller = new BotController(game, bot_player, this);
				controller.SetNextState(new BotWanderState());
				return controller;
			}

			private BasePlayer CreateZombie(GameManager game, Vector3 position, Vector3 look, string name = "Idiot")
			{
				BasePlayer ai_bot = Utilities.CreatePlayer(position, look, name);

				game.timer.In(0.2f, () =>
				{
					ai_bot._displayName = name;
				});

				ai_bot.inventory.Strip();

				foreach (int weapon in weapons)
					ai_bot.GiveWeapon(weapon);

				foreach (int item in attire)
					ai_bot.GiveAttire(item);

				if (!ai_bot.inventory.containerBelt.IsEmpty())
					ai_bot.SwitchWeapon(0);

				BaseProjectile gun = ai_bot.GetHeldEntity() as BaseProjectile;

				if (gun != null)
					gun.TopUpAmmo();

				return ai_bot;
			}

			public float WalkTickDistance() => (walk_speed_s / 10.0f);
			public float RunTickDistance() => (run_speed_s / 10.0f);
		}

		public class BotController
		{
			public GameManager game { get; private set; }
			public BotConfiguration configuration { get; private set; }
			public BasePlayer bot_player { get; private set; } = null;
			public Vector3 look_dir = new Vector3(1.0f, 0.0f, 0.0f);
			public Vector3 tick_velocity = new Vector3(1.0f, 0.0f, 0.0f);

			private Collider[] collider_buffer = new Collider[32];

			private BotAIState current_state;
			private BotAIState next_state;

			public BotController(GameManager game, BasePlayer bot_player, BotConfiguration configuration)
			{
				this.game = game;
				this.bot_player = bot_player;
				this.configuration = configuration;
			}

			public void SetNextState(BotAIState next) => next_state = next;

			public void OnTick()
			{
				BotAIState state = CurrentState();

				Vector3 pre_tick_position = new Vector3();

				bool is_alive = bot_player.IsAlive();

				if (is_alive)
					pre_tick_position = bot_player.transform.position;

				if (configuration.should_tick)
					state?.OnTick();

				if (is_alive)
				{
					Vector3 post_tick_position = bot_player.transform.position;
					tick_velocity = post_tick_position - pre_tick_position;
				}
			}

			public BotAIState CurrentState()
			{
				HandleTransition();
				return current_state;
			}

			private void HandleTransition()
			{
				if (next_state != null)
				{
					current_state?.OnExit();
					current_state = next_state;
					current_state.SetRoots(game, this);
					current_state.OnEnter();
					next_state = null;
				}
			}

			public void DoAttack(BasePlayer target_player)
			{
				if (IsHoldingMeleeWeapon())
					DoMeleeAttack(target_player);

				if (IsHoldingProjectileWeapon())
					DoProjectileAttack(target_player);
			}

			public bool IsHoldingMeleeWeapon()
			{
				BaseMelee melee = bot_player.GetHeldEntity() as BaseMelee;
				return (melee != null);
			}

			public bool IsHoldingProjectileWeapon()
			{
				BaseProjectile projectile = bot_player.GetHeldEntity() as BaseProjectile;
				return (projectile != null);
			}

			private void GetLOSRaycastOrigins(List<Vector3> origins)
			{
				Vector3 headright = bot_player.eyes.HeadRight() * 2.0f;

				origins.Add(bot_player.eyes.position);
				origins.Add(bot_player.eyes.position + headright);
				origins.Add(bot_player.eyes.position - headright);
				origins.Add((bot_player.transform.position + bot_player.eyes.position) / 2.0f);

				BaseProjectile projectile = bot_player.GetHeldEntity() as BaseProjectile;

				if (projectile != null)
					origins.Add(projectile.MuzzlePoint.position);
			}

			public bool HasAcceptableAttackLOS(BasePlayer target_player, bool require_hit_exact_player = false)
			{
				RaycastHit hit;
				Vector3 target = target_player.eyes.position;
				List<Vector3> origins = Pool.GetList<Vector3>();
				GetLOSRaycastOrigins(origins);
				int hits = 0;

				foreach (Vector3 origin in origins)
				{
					Physics.Raycast(origin, target - origin, out hit, 300.0f);

					if (hit.collider != null)
					{
						if (require_hit_exact_player && hit.GetEntity() == target_player)
							hits++;

						if (!require_hit_exact_player && hit.GetEntity() is BasePlayer && hit.GetEntity() != bot_player)
							hits++;
					}
				}

				bool ret = (hits >= origins.Count);
				Pool.FreeList<Vector3>(ref origins);
				return ret;
			}

			public void DoMeleeAttack(BasePlayer target_player)
			{
				BaseMelee melee = bot_player.GetHeldEntity() as BaseMelee;

				if (melee == null)
					throw new Exception("DoMeleeAttack invoked on a BasePlayer holding a projectile weapon?");

				bool weapon_is_good = (melee != null && !melee.HasAttackCooldown());

				if (weapon_is_good)
				{
					melee.StartAttackCooldown(melee.repeatDelay);
					bot_player.SignalBroadcast(BaseEntity.Signal.Attack, "", null);

					game.timer.In(0.3f, () =>
					{
						if (bot_player.IsAlive() && target_player.IsAlive())
						{
							bool is_still_within_range = (Vector3.Distance(bot_player.transform.position, target_player.transform.position) < configuration.attack_distance);

							if (is_still_within_range)
							{
								HitInfo info = Pool.Get<HitInfo>();

								info.DoHitEffects = true;
								info.DoDecals = true;
								info.UseProtection = true;
								info.damageTypes = Pool.Get<DamageTypeList>();
								info.gatherScale = 1.0f;
								info.Initiator = bot_player;
								info.HitEntity = target_player;
								info.HitPositionWorld = target_player.transform.position;
								info.PointStart = bot_player.transform.position;
								info.damageTypes.Add(DamageType.Slash, configuration.damage);
								info.HitMaterial = StringPool.Get("flesh");

								target_player.OnAttacked(info);
								Effect.server.ImpactEffect(info);

								Pool.Free<DamageTypeList>(ref info.damageTypes);
								Pool.Free<HitInfo>(ref info);
							}
						}
					});
				}
			}

			public void DoProjectileAttack(BasePlayer target_player)
			{
				BaseProjectile gun = bot_player.GetHeldEntity() as BaseProjectile;

				if (gun == null)
					throw new Exception("FireWeapon invoked on a BasePlayer holding a melee weapon?");

				bool is_loaded = (gun.primaryMagazine.contents > 0);
				bool has_cooldown = (gun.HasAttackCooldown() || gun.HasReloadCooldown());

				if (!has_cooldown && is_loaded)
				{
					gun.StartAttackCooldown(gun.ScaleRepeatDelay(gun.repeatDelay) + gun.animationDelay);
					gun.primaryMagazine.contents--;
					gun.SignalBroadcast(BaseEntity.Signal.Attack, "", null);
					CurrentState()?.OnStartFiringProjectileWeapon();
					CreateProjectiles(gun);
				}

				if (!has_cooldown && !is_loaded)
				{
					gun.StartReloadCooldown(gun.reloadTime);
					gun.TopUpAmmo();
					bot_player.SignalBroadcast(BaseEntity.Signal.Reload, "", null);
					CurrentState()?.OnStopFiringProjectileWeapon();
				}
			}

			public void CreateProjectiles(BaseProjectile gun)
			{
				ItemModProjectile modifier = gun.primaryMagazine.ammoType.GetComponent<ItemModProjectile>();
				Projectile projectile = modifier.projectileObject.Get().GetComponent<Projectile>();

				for (int i = 0; i < modifier.numProjectiles; i++)
				{
					Vector3 gunfire_direction = AimConeUtil.GetModifiedAimConeDirection(modifier.projectileSpread + gun.aimCone, look_dir, true);

					List<RaycastHit> hits = Pool.GetList<RaycastHit>();
					GamePhysics.TraceAll(new Ray(bot_player.eyes.position, gunfire_direction), 0.0f, hits, 300f, 1084435201, QueryTriggerInteraction.UseGlobal);

					foreach (RaycastHit hit in hits)
					{
						DoProjectileHitEffect(gun, hit, gunfire_direction);

						BaseEntity entity = hit.GetEntity();

						if (entity != null && entity is BaseCombatEntity)
						{
							float damage = 0.0f;

							foreach (Rust.DamageTypeEntry dmg_entry in projectile.damageTypes)
								damage += dmg_entry.amount;

							BaseCombatEntity bce = entity as BaseCombatEntity;

							bce.Hurt(damage * gun.npcDamageScale, Rust.DamageType.Bullet, bot_player, gun);
						}
					}

					Pool.FreeList<RaycastHit>(ref hits);
				}
			}

			private void DoProjectileHitEffect(BaseProjectile gun, RaycastHit hit, Vector3 gunfire_direction)
			{
				//StringBuilder sb = new StringBuilder();

				HitInfo info = Pool.Get<HitInfo>();

				try
				{
					info.Initiator = bot_player;
					info.Weapon = gun;
					info.WeaponPrefab = gun.gameManager.FindPrefab(gun.PrefabName).GetComponent<AttackEntity>();
					info.IsPredicting = false;
					info.DoHitEffects = true;
					info.DidHit = true;
					info.ProjectileVelocity = gunfire_direction * 3.0f;
					info.PointStart = gun.MuzzlePoint.position;
					info.PointEnd = hit.point;
					info.HitPositionWorld = hit.point;
					info.HitNormalWorld = hit.normal;
					info.HitEntity = hit.GetEntity();
					info.UseProtection = true;

					//sb.AppendLine($"initiator: {info.Initiator} weapon: {info.Weapon} weapon prefab: {info.WeaponPrefab} hit ent: {info.HitEntity}");

					Effect.server.ImpactEffect(info);
				}
				catch (Exception e)
				{
					//game.debug(e.ToString());
					//game.debug(sb.ToString());
				}
				Pool.Free<HitInfo>(ref info);
			}

			public bool ApplyEnvironmentPushVector()
			{
				Vector3 push_vector = GetEnvironmentPushVector();
				push_vector.y = 0.0f;

				if (push_vector != Vector3.zero)
				{
					bot_player.transform.position += push_vector;
					return true;
				}

				return false;
			}

			private Vector3 GetEnvironmentPushVector()
			{
				Vector3 ret = Vector3.zero;
				Vector3 spherecast_point = ((bot_player.transform.position + bot_player.eyes.position) / 2.0f);

				int collider_count = Physics.OverlapSphereNonAlloc(spherecast_point, bot_player.playerCollider.radius * 0.5f, collider_buffer);

				for (int i = 0; i < collider_count; i++)
				{
					Collider collider = collider_buffer[i];
					BasePlayer collider_player = collider.GetComponentInParent<BasePlayer>();

					bool collider_is_not_me = (collider_player != null && collider_player != bot_player);
					bool collider_is_valid = (collider is BoxCollider || collider is SphereCollider || collider is CapsuleCollider);

					if (collider_is_not_me && collider_is_valid)
					{
						Vector3 contact = collider.ClosestPoint(spherecast_point);
						Vector3 difference = spherecast_point - contact;
						ret += difference;
					}
				}

				if (ret == Vector3.zero)
					return ret;

				ret = ret.normalized;
				ret *= configuration.WalkTickDistance();
				return ret;
			}

			public List<BasePlayer> GetPlayersInSphere(Vector3 center, float radius)
			{
				List<BasePlayer> output = new List<BasePlayer>();
				int collider_count = Physics.OverlapSphereNonAlloc(center, radius, collider_buffer);

				for (int i = 0; i < collider_count; i++)
				{
					Collider collider = collider_buffer[i];

					BasePlayer player = collider.GetComponentInParent<BasePlayer>();

					if (player != null && player.IsAlive() && player != bot_player)
						output.Add(player);
				}

				return output;
			}

			public bool MoveIfNotBlocked(Vector3 current_position, Vector3 new_position)
			{
				Vector3 movement_direction = (new_position - current_position).normalized;

				List<BasePlayer> players = GetPlayersInSphere(new_position, bot_player.playerCollider.radius);
				List<Vector3> player_positions = new List<Vector3>();

				foreach (BasePlayer player in players)
					player_positions.Add(player.transform.position);

				bool is_blocked = false;
				float capsule_radius = bot_player.playerCollider.radius;

				foreach (Vector3 player_position in player_positions)
				{
					Vector3 direction = (player_position - current_position).normalized;
					float dot = Vector3.Dot(movement_direction, direction);
					float dist = Vector3.Distance(player_position, current_position);

					bool is_far_enough_away = (dist > capsule_radius);
					bool is_ahead = (dot > 0.9f);

					if (is_far_enough_away && is_ahead)
					{
						is_blocked = true;
						break;
					}
				}

				if (!is_blocked)
					bot_player.transform.position = new_position;

				return is_blocked;
			}
		}

		public abstract class BotAIState
		{
			protected GameManager game;
			protected BotController controller;
			private float last_path_request_time = 0.0f;
			private float last_path_received_time = 0.0f;
			private bool is_waiting_for_path = false;

			public void SetRoots(GameManager game, BotController controller)
			{
				this.game = game;
				this.controller = controller;
			}

			protected void OnPathRequested()
			{
				last_path_request_time = UnityEngine.Time.realtimeSinceStartup;
				is_waiting_for_path = true;
			}

			protected void OnPathReceived()
			{
				last_path_received_time = UnityEngine.Time.realtimeSinceStartup;
				is_waiting_for_path = false;
			}

			protected float TimeSinceLastPathRequest() =>
				 UnityEngine.Time.realtimeSinceStartup - last_path_request_time;

			protected float TimeSinceLastPathReceived() =>
				 UnityEngine.Time.realtimeSinceStartup - last_path_received_time;

			protected bool IsWaitingForPath() =>
				 is_waiting_for_path;

			public virtual void OnEnter() { }
			public virtual void OnExit() =>
				 OnStopFiringProjectileWeapon();

			public virtual void OnTick()
			{
				if (controller.bot_player.IsAlive())
					controller.bot_player.SendNetworkUpdate();
			}

			public virtual void OnTakeDamage(HitInfo info) { }

			public virtual void OnStartFiringProjectileWeapon() =>
				 controller.bot_player.modelState.aiming = true;

			public virtual void OnStopFiringProjectileWeapon() =>
				 controller.bot_player.modelState.aiming = false;
		}

		public class BotWanderState : BotAIState
		{
			private Vector3 wander_target_point;
			private PathFollower path_follower = null;
			private PathfindingOptions pathfinding_options;
			private float wander_distance = 0.0f;

			public override void OnEnter()
			{
				FindNewWanderPoint();

				base.OnEnter();
			}

			public override void OnTick()
			{
				if (controller.configuration.on_detect_player != null && controller.configuration.check_for_nearby_players != null)
				{
					BasePlayer player = controller.configuration.check_for_nearby_players(game, controller);

					if (player != null)
						controller.configuration.on_detect_player(game, controller, player);
				}

				float target_distance = Vector3.Distance(wander_target_point, controller.bot_player.transform.position);
				bool is_close_enough_to_target = (target_distance < controller.configuration.wander_target_swapover_distance);
				bool have_reached_end_of_path = (path_follower != null && wander_distance >= path_follower.total_cost);
				bool needs_path_update = (is_close_enough_to_target || have_reached_end_of_path);
				bool is_waiting_for_path = IsWaitingForPath();

				if (needs_path_update && !is_waiting_for_path)
					FindNewWanderPoint();

				if (!needs_path_update)
					DoLocomotion();

				base.OnTick();
			}

			public override void OnTakeDamage(HitInfo info)
			{
				if (info.Initiator is BasePlayer && controller.configuration.on_detect_player != null)
					controller.configuration.on_detect_player(game, controller, info.Initiator as BasePlayer);

				base.OnTakeDamage(info);
			}

			private void DoLocomotion()
			{
				if (path_follower == null)
					return;

				wander_distance += controller.configuration.WalkTickDistance();

				Vector3 current_position = controller.bot_player.transform.position;
				Vector3 locomotion_target;
				Vector3 look;

				path_follower.GetTransform(wander_distance, 10, 0.05f, out locomotion_target, out look);

				current_position = Vector3.MoveTowards(current_position, locomotion_target, controller.configuration.WalkTickDistance() + 0.5f);

				controller.bot_player.transform.position = current_position;
				controller.look_dir = look;
			}

			private Vector3 FindDifferentWanderPoint(Vector3 current_wander_point)
			{
				Vector3 new_wander_target = current_wander_point;
				int attempts = 0;

				while (new_wander_target == current_wander_point && attempts < 10)
				{
					attempts++;
					new_wander_target = game.spawner.GetRandomSpawnPoint(game.random).location.Vector3();
				}

				return new_wander_target;
			}

			private void FindNewWanderPoint()
			{
				if (controller.configuration.create_pathfinding_options != null)
					pathfinding_options = controller.configuration.create_pathfinding_options(game);
				else
					pathfinding_options = PathfindingOptions.RandomPerlin();

				PathFollower follower = null;
				Vector3 origin = controller.bot_player.transform.position;

				wander_target_point = FindDifferentWanderPoint(wander_target_point);

				Action job = () =>
				{
					follower = game.nav.GetPathFollower(origin, wander_target_point, pathfinding_options);
				};

				Action callback = () =>
				{
					path_follower = follower;
					wander_distance = 0.0f;
					OnPathReceived();
				};

				OnPathRequested();
				game.thread_pool.EnqueueJob(job, callback);
			}
		}

		public class BotPursuitState : BotAIState
		{
			private BasePlayer target_player;
			private List<Vector3> target_points;
			private PathfindingOptions pathfinding_options;
			private bool is_obstructed = false;
			private int ticks_until_nav_update = 0;

			public BotPursuitState(BasePlayer target_player)
			{
				this.target_player = target_player;
				pathfinding_options = PathfindingOptions.Default();
			}

			private Vector3 GetTargetPoint()
			{
				Vector3 current_position = controller.bot_player.transform.position;

				if (target_points == null || target_points.Count < 1)
					return current_position;

				Vector3 target_point = target_points[0];

				float distance = Vector3.Distance(target_point, current_position);

				if (target_points.Count > 1 && distance < controller.configuration.RunTickDistance())
				{
					target_point = target_points[1];
					target_points.RemoveAt(0);
				}

				return target_point;
			}

			public override void OnTick()
			{
				if (!controller.bot_player.IsAlive())
				{
					base.OnTick();
					return;
				}

				if (!target_player.IsAlive() && controller.configuration.on_target_player_death != null)
				{
					controller.configuration.on_target_player_death(game, controller);
					base.OnTick();
					return;
				}

				ticks_until_nav_update--;

				if (ticks_until_nav_update < 1)
				{
					UpdateNavigation();
					float distance = Vector3.Distance(controller.bot_player.transform.position, target_player.transform.position);
					ticks_until_nav_update = (int)(distance / 10.0f);
				}

				DoLocomotion();

				base.OnTick();
			}

			private void DoLocomotion()
			{
				Vector3 target_point = GetTargetPoint();

				if (target_point != controller.bot_player.transform.position)
					controller.look_dir = (target_point - controller.bot_player.transform.position).normalized;

				Vector3 new_position = controller.bot_player.transform.position;
				Vector3 current_position = controller.bot_player.transform.position;

				float current_distance = Vector3.Distance(controller.bot_player.transform.position, target_player.transform.position);
				float tick_distance = controller.configuration.RunTickDistance();
				bool is_close_enough_to_attack = (current_distance < controller.configuration.attack_distance);
				bool should_pursue = (current_distance > controller.configuration.stop_pursuit_distance);
				bool has_los = (is_close_enough_to_attack && controller.HasAcceptableAttackLOS(target_player));

				if (is_close_enough_to_attack && has_los)
				{
					controller.look_dir = (target_player.transform.position - controller.bot_player.transform.position).normalized;
					controller.DoAttack(target_player);
					OnStartFiringProjectileWeapon();

					if (should_pursue)
					{
						tick_distance = controller.configuration.WalkTickDistance();
						new_position = Vector3.MoveTowards(new_position, target_point, tick_distance);
					}
				}

				if (!is_close_enough_to_attack || !has_los)
				{
					new_position = Vector3.MoveTowards(new_position, target_point, tick_distance);
					OnStopFiringProjectileWeapon();
				}

				controller.bot_player.transform.position = new_position;

				controller.ApplyEnvironmentPushVector();

				controller.MoveIfNotBlocked(current_position, new_position);
			}

			private void UpdateNavigation()
			{
				if (!controller.bot_player.IsAlive() || !target_player.IsAlive())
					return;

				bool origin_point_known = game.last_recorded_mesh_positions.ContainsKey(controller.bot_player);
				bool target_point_known = game.last_recorded_mesh_positions.ContainsKey(target_player);

				if (!origin_point_known || !target_point_known)
					return;

				int origin_point_id = game.last_recorded_mesh_positions[controller.bot_player];
				int target_point_id = game.last_recorded_mesh_positions[target_player];

				if (origin_point_id == target_point_id)
					return;

				Vector3 current_position = controller.bot_player.transform.position;
				Vector3 target_position = target_player.transform.position;

				bool should_use_obstructions = (Vector3.Distance(current_position, target_position) < 50.0f);

				CreatePathJob(current_position, origin_point_id, target_point_id, should_use_obstructions);
			}

			private void CreatePathJob(Vector3 current_position, int origin_point_id, int target_point_id, bool should_use_obstructions)
			{
				List<Vector3> points = null;

				if (should_use_obstructions)
					pathfinding_options.obstruction_provider = BuildObstructionProvider(origin_point_id, target_point_id, target_player);
				else
					pathfinding_options.obstruction_provider = null;

				is_obstructed = (pathfinding_options.obstruction_provider != null && pathfinding_options.obstruction_provider.IsPointObstructed(target_point_id, null));

				if (is_obstructed)
					return;

				Action job = () =>
				{
					Func<Vector3, bool> should_continue = (Vector3 point) =>
					{
						float distance = Vector3.Distance(point, current_position);
						bool ret = (distance < 10.0f);
						return ret;
					};

					points = game.nav.GetSmoothedPath(origin_point_id, target_point_id, 0.01f, pathfinding_options, should_continue, null);
				};

				Action callback = () =>
				{
					bool no_path = (points != null && points.Count > 0 && points.Count < 2 && points[0] == game.nav.GetPoint(origin_point_id));

					if (points != null && points.Count > 0)
						target_points = points;

					OnPathReceived();
				};

				OnPathRequested();
				game.thread_pool.EnqueueJob(job, callback);
			}

			private IObstructionProvider BuildObstructionProvider(int origin_id, int destination_id, BasePlayer target_player)
			{
				IObstructionProvider ret;
				float obstruction_radius = controller.bot_player.playerCollider.radius;
				//obstruction_radius *= 1.5f;

				List<BasePlayer> nearby_players = controller.GetPlayersInSphere(controller.bot_player.transform.position, 10.0f);
				List<Vector3> obstructions = new List<Vector3>();

				foreach (BasePlayer player in nearby_players)
					if (player != target_player)
						obstructions.Add(player.transform.position);

				ret = new ObstructionProvider(game, obstructions, obstruction_radius, game.nav, origin_id, destination_id, true);

				return ret;
			}
		}

		public class ObstructionProvider : IObstructionProvider
		{
			private GameManager game;
			public List<Vector3> obstructed_positions;
			private float obstruction_radius;
			private IPointAdapter point_adapter;
			private Vector3 origin_point;
			private Vector3 destination_point;
			private int origin_id;
			private int destination_id;
			private float max_distance;
			private Vector3 center_point;
			private bool perform_distance_test;

			public ObstructionProvider(GameManager game, List<Vector3> obstructed_positions, float obstruction_radius, IPointAdapter point_adapter, int origin_id, int destination_id, bool perform_distance_test)
			{
				this.game = game;
				this.obstructed_positions = obstructed_positions;
				this.obstruction_radius = obstruction_radius;
				this.point_adapter = point_adapter;
				this.origin_id = origin_id;
				this.destination_id = destination_id;
				origin_point = point_adapter.GetPoint(origin_id);
				destination_point = point_adapter.GetPoint(destination_id);
				max_distance = Vector3.Distance(origin_point, destination_point) * 4.0f;
				center_point = (origin_point + destination_point) / 2.0f;
				this.perform_distance_test = perform_distance_test;
			}

			public bool IsPointObstructed(int point_id, Action<object> debug)
			{
				Vector3 point = point_adapter.GetPoint(point_id);
				bool ret = IsPointObstructed(point, debug);
				return ret;
			}

			public bool IsPointObstructed(Vector3 point, Action<object> debug)
			{
				if (perform_distance_test && IsTooFarFromCenterPoint(point))
					return true;

				foreach (Vector3 obstruction in obstructed_positions)
				{
					float distance = Vector3.Distance(point, obstruction);

					if (distance < obstruction_radius)
						return true;
				}

				return false;
			}

			public bool IsTooFarFromCenterPoint(Vector3 point)
			{
				float distance = Vector3.Distance(point, center_point);
				bool ret = (distance > max_distance);
				return ret;
			}
		}

		public class GameManager
		{
			public ThreadPool thread_pool = new ThreadPool();
			public NavData nav;
			public Spawner spawner;
			public Dictionary<BasePlayer, BotController> bot_controllers = new Dictionary<BasePlayer, BotController>();
			public Dictionary<BasePlayer, int> last_recorded_mesh_positions = new Dictionary<BasePlayer, int>();
			public Dictionary<BasePlayer, int> obstruction_ids = new Dictionary<BasePlayer, int>();
			public Action<string> debug = null;
			public PluginTimers timer;
			public System.Random random;
			public BotConfiguration melee_configuration { get; private set; } = BotConfiguration.MeleePursuer();
			public BotConfiguration db_configuration { get; private set; } = BotConfiguration.DoubleBarrel();
			public Action<Action> next_tick;
			public Dictionary<BasePlayer, Vector3> player_positions = new Dictionary<BasePlayer, Vector3>();
			private ConcurrentQueue<object> debug_queue = new ConcurrentQueue<object>();
			public PerformanceReport debug_file = PerformanceReport.CurrentTime();

			public GameManager(Action<string> debug, Action<Action> next_tick, PluginTimers timer)
			{
				this.debug = debug;
				this.next_tick = next_tick;
				this.timer = timer;
				this.random = new System.Random();
			}

			public void MultithreadedDebug(object dbg)
			{
				debug_queue.Enqueue(dbg);
			}

			public void OnPlayerDeath(BasePlayer player)
			{
				next_tick(() =>
				{
					bot_controllers.Remove(player);
					last_recorded_mesh_positions.Remove(player);
				});
			}

			public BotController SpawnBot(BotConfiguration configuration)
			{
				SpawnPoint sp = spawner.GetRandomSpawnPoint(random);
				BotController controller = SpawnBot(configuration, sp.location.Vector3());
				bot_controllers[controller.bot_player] = controller;
				return controller;
			}

			public BotController SpawnBot(BotConfiguration configuration, Vector3 position)
			{
				BotController controller = configuration.CreateBot(this, position, new Vector3(1.0f, 0.0f, 0.0f));
				bot_controllers[controller.bot_player] = controller;
				return controller;
			}

			public void ClearDebugQueue()
			{
				object obj;

				while (debug_queue.TryDequeue(out obj))
				{
					if (obj is Vector3)
						Utilities.DrawAABB(BasePlayer.activePlayerList[0], (Vector3)obj, new Vector3(0.2f, 0.2f, 0.2f), 0.1f);

					if (obj is Ray)
					{
						Ray ray = (Ray)obj;
						BasePlayer player = BasePlayer.activePlayerList[0];
						debug($"{ray.origin}");
						debug($"{ray.direction}");
						player.SendConsoleCommand("ddraw.line", 0.1f, Color.white, ray.origin, ray.origin + (ray.direction * 5.0f));
					}

					if (obj is string)
						debug((string)obj);
				}
			}

			public void Tick()
			{
				thread_pool.ProcessCallbacks((object o) => { debug(o.ToString()); });

				if (nav == null)
					return;

				player_positions.Clear();
				List<BasePlayer> update_list = new List<BasePlayer>(BasePlayer.activePlayerList);
				update_list.AddRange(bot_controllers.Keys);

				foreach (BasePlayer player in update_list)
				{
					if (player.IsAlive())
					{
						int point_id = nav.GetPoint(player.transform.position, 1.0f);

						if (point_id != -1)
							last_recorded_mesh_positions[player] = point_id;

						player_positions[player] = player.transform.position;
					}
				}

				foreach (BasePlayer bot in bot_controllers.Keys)
				{
					if (bot.IsAlive())
					{
						BotController controller = bot_controllers[bot];
						controller.OnTick();
					}
				}

				ClearDebugQueue();
			}
		}

		private GameManager game;

		object OnItemCreated(ItemDefinition itdef, int amount, ulong skin)
		{
			return null;
		}

		void Init()
		{
			game = new GameManager((string s) => { Puts(s); }, (Action action) => { NextTick(action); }, timer);
		}

		void Unload()
		{
			game.thread_pool.Shutdown();

			game.debug_file.Dispose();

			foreach (BasePlayer bot in game.bot_controllers.Keys)
				bot.Kill();
		}

		Vector3 GetHeliTargetLocation(Vector3 orbit_point, float time, float orbit_radius, float orbit_height)
		{
			Vector3 target_location = new Vector3();

			target_location.x = Mathf.Sin(time / 10.0f);
			target_location.z = Mathf.Cos(time / 10.0f);

			target_location.Normalize();

			target_location *= orbit_radius;

			target_location += orbit_point;
			target_location.y = orbit_point.y + orbit_height;

			return target_location;
		}

		object OnHelicopterAIUpdate(PatrolHelicopterAI helicopter_ai, BaseHelicopter helicopter)
		{
			if (BasePlayer.activePlayerList.Count < 1)
				return null;

			float time = UnityEngine.Time.realtimeSinceStartup;

			BasePlayer target_player = BasePlayer.activePlayerList[0];

			Vector3 target_point = GetHeliTargetLocation(target_player.transform.position, time, 30.0f, 40.0f);
			Vector3 target_look = GetHeliTargetLocation(target_player.transform.position, time + 1.0f, 30.0f, 40.0f);
			Quaternion look_rotation = Quaternion.LookRotation(target_look - target_point);

			helicopter_ai.SetIdealRotation(look_rotation);
			helicopter_ai.SetTargetDestination(target_point);
			helicopter_ai.MoveToDestination();
			helicopter_ai.UpdateRotation();
			helicopter_ai.UpdateSpotlight();

			helicopter.spotlightTarget = target_player.transform.position;

			//Puts(target_player._displayName);

			return false;
		}

		void OnTick() => game.Tick();
		object CanDropActiveItem(BasePlayer player) => false;
		void OnEntitySpawned(DroppedItemContainer container) => container.Kill();
		void OnPlayerDeath(BasePlayer player) => game.OnPlayerDeath(player);
		object OnEntityTakeDamage(BaseHelicopter heli, HitInfo info) => false;

		object OnEntityTakeDamage(BaseCombatEntity entity, HitInfo info)
		{
			BasePlayer player = entity as BasePlayer;

			if (player != null && game.bot_controllers.ContainsKey(player))
			{
				BotController controller = game.bot_controllers[player];
				BotAIState state = controller.CurrentState();
				state.OnTakeDamage(info);
			}

			return null;
		}

		object OnPlayerWound(BasePlayer player)
		{
			if (BasePlayer.activePlayerList.Contains(player))
				return null;

			return false;
		}

		void OnPlayerCorpse(BasePlayer player, PlayerCorpse corpse)
		{
			if (!BasePlayer.activePlayerList.Contains(player))
				timer.In(3.0f, () => { corpse.Kill(); });
		}

		object OnGetNetworkRotation(BasePlayer player)
		{
			if (!game.bot_controllers.ContainsKey(player))
				return null;

			BotController controller = game.bot_controllers[player];
			Quaternion look_quat = Quaternion.LookRotation(controller.look_dir, Vector3.up);
			return look_quat;
		}

		[ConsoleCommand("zb_spawnbots")]
		void ccmdSpawnBot(ConsoleSystem.Arg args)
		{
			BotConfiguration pause_config = BotConfiguration.MeleePursuer();
			pause_config.check_for_nearby_players = null;
			pause_config.name = "testy boi";
			pause_config.should_tick = false;

			BotConfiguration wander_only = BotConfiguration.MeleePursuer();
			wander_only.check_for_nearby_players = null;

			game.SpawnBot(game.melee_configuration);
		}

		[ConsoleCommand("zb_testboi")]
		void ccmdSpawnTestBoi(ConsoleSystem.Arg args)
		{
			BotConfiguration testboi = BotConfiguration.MeleePursuer();
			testboi.attire.Clear();
			testboi.weapons.Clear();
			testboi.name = "testboi";
			testboi.on_detect_player = null;
			game.SpawnBot(testboi);
		}

		[ConsoleCommand("zb_loadspawner")]
		void ccmdLoadSpawnPoints(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			string filename = args.Args[0];
			game.spawner = Spawner.LoadFile(filename);

			if (game.spawner != null)
				player.SendDebugMessage($"loaded spawner file: {filename}");
			else
				player.SendDebugMessage($"could not load spawner file: {filename}");

			if (game.nav == null)
			{
				player.SendDebugMessage("game.nav was null, zombies will not move and spawn points will not be verified!");
				return;
			}

			List<int> to_remove = new List<int>();

			for (int i = 0; i < game.spawner.spawn_points.Count; i++)
			{
				SpawnPoint sp = game.spawner.spawn_points[i];
				int mesh_point_id = game.nav.GetPoint(sp.location.Vector3());

				if (mesh_point_id == -1)
				{
					string msg = $"spawn point: {i} was not on the mesh, removing...";
					player.SendDebugMessage(msg);
					to_remove.Add(i);
				}
			}

			to_remove.Reverse();

			foreach (int i in to_remove)
				game.spawner.spawn_points.RemoveAt(i);
		}

		[ConsoleCommand("zb_loadnav")]
		void ccmdLoadNav(ConsoleSystem.Arg args)
		{
			BasePlayer player = args.Player();
			string filename = args.Args[0];
			player.ChatMessage($"loading nav file {filename}");
			NavData file = null;

			Action job = () => { file = NavData.Load(filename); };
			Action callback = () =>
			{
				if (file == null)
				{
					player.ChatMessage($"{filename} nav file could not be loaded");
					return;
				}

				game.nav = file;
				player.ChatMessage($"nav file {filename} loaded!");
			};

			game.thread_pool.EnqueueJob(job, callback);
		}
	}
}