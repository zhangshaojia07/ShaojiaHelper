using System.Collections;
using Celeste.Mod.Entities;
using FMOD.Studio;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.ShaojiaHelper.Entities {

    [CustomEntity("ShaojiaHelper/CrusherKevin")]
    public class CrusherKevin : Solid {
        public enum Axes
		{
			Both,
			Horizontal,
			Vertical
		}

		private struct MoveState
		{
			public Vector2 From;

			public Vector2 Direction;

			public MoveState(Vector2 from, Vector2 direction)
			{
				From = from;
				Direction = direction;
			}
		}

		private const float CrushSpeed = 240f;
		private const float CrushAccel = 500f;
		private const float ReturnSpeed = 60f;
		private const float ReturnAccel = 160f;

		private const int SfxIntervalLeniency = 3;
		private const int SfxSightLeniency = 3;
		private const float ImpactVolumeThreshold = 0.2f;
		// Temporary ML volume; tune this value later.
		private const float MoveLoopSfxVolume = 0.5f;

		// SFX debug switches.
		private static readonly bool EnableAttackSfx = true;
		private static readonly bool EnableImpactSfx = true;
		private static readonly bool EnableRestSfx = true;
		private static readonly bool EnableRestWaypointSfx = true;
		private static readonly bool EnableMoveLoopSfx = false;
		private static readonly bool EnableReturnLoopSfx = false;

		private Color fill = Calc.HexToColor("62222b");

		private Level level = null!;

		private Vector2 crushDir;
		private Vector2 lastCrushDir;

		private List<MoveState> returnStack;

		private Coroutine attackCoroutine;

		private bool canActivate;
		private bool canMoveUp;
		private bool canMoveDown;
		private bool canMoveLeft;
		private bool canMoveRight;
		private bool chillOut;
		private bool coolingDown;
		private bool giant;
		private bool requiresLineOfSight;
		private bool interruptable;
		private bool nonReturn;

		private Sprite face;

		private string nextFaceDirection = "idle";

		private List<Image> idleImages = new List<Image>();
		private List<Image> activeTopImages = new List<Image>();
		private List<Image> activeRightImages = new List<Image>();
		private List<Image> activeLeftImages = new List<Image>();
		private List<Image> activeBottomImages = new List<Image>();

		private EventInstance? currentMoveLoopSfx;
		private bool currentMoveLoopSfxIs3D;
		private readonly List<EventInstance> endingMoveLoopSfx = new List<EventInstance>();
		private SoundSource returnLoopSfx;

		private int framesSinceLastAttack = 0;
		// Updated after the player scan; Attack() reads the value from the previous frame.
		private int framesSincePlayerSeen = int.MaxValue;

		private bool Submerged => base.Scene.CollideCheck<Water>(new Rectangle((int)(base.Center.X - 4f), (int)base.Center.Y, 8, 4));

		private bool Returnable => !nonReturn;

		private void PlayOneShotSfx(string eventPath, Vector2 position, bool enabled)
		{
			if (!enabled)
			{
				return;
			}

			Audio.Play(eventPath, position);
		}

		private void PlayImpactSfx(Vector2 position, float volume)
		{
			if (!EnableImpactSfx)
			{
				return;
			}

			var sfx = Audio.Play("event:/game/06_reflection/crushblock_impact", position);
			if (sfx != null)
			{
				sfx.setVolume(volume);
			}
		}

		private float GetImpactSfxVolume(float impactSpeed)
		{
			if (chillOut)
			{
				return 1f;
			}

			// Linear for now; change this line if the volume curve needs tuning.
			return Calc.Clamp(impactSpeed / CrushSpeed, 0f, 1f);
		}

		private bool HasMovedSinceAttack(Vector2 attackStartPosition)
		{
			if (crushDir.X != 0f)
			{
				return Position.X != attackStartPosition.X;
			}
			if (crushDir.Y != 0f)
			{
				return Position.Y != attackStartPosition.Y;
			}
			return Position != attackStartPosition;
		}

		private EventInstance? PlayMoveLoopSfx(string eventPath)
		{
			EventDescription? description = Audio.GetEventDescription(eventPath);
			if (description == null)
			{
				return null;
			}

			bool is3D = false;
			description.is3D(out is3D);
			currentMoveLoopSfxIs3D = is3D;

			EventInstance? sfx = Audio.CreateInstance(eventPath, Center);
			if (sfx == null)
			{
				return null;
			}

			// Set volume before starting so the loop never begins at full volume.
			sfx.setVolume(MoveLoopSfxVolume);
			sfx.start();
			return sfx;
		}

		private void EndMoveLoopSfx(EventInstance sfx)
		{
			Audio.SetParameter(sfx, "end", 1f);
			endingMoveLoopSfx.Add(sfx);
			Alarm.Set(this, 0.5f, delegate
			{
				if (endingMoveLoopSfx.Remove(sfx))
				{
					Audio.Stop(sfx);
				}
			});
		}

		private void StopMoveLoopSfx(bool allowFadeOut)
		{
			if (currentMoveLoopSfx != null)
			{
				Audio.Stop(currentMoveLoopSfx, allowFadeOut);
				currentMoveLoopSfx = null;
			}

			foreach (EventInstance sfx in endingMoveLoopSfx)
			{
				Audio.Stop(sfx, allowFadeOut);
			}
			endingMoveLoopSfx.Clear();
		}

		private bool ShouldSuppressAttackSfx(Vector2 direction)
		{
			// Kevin saw a player very recently, and this trigger repeats
			// the previous attack direction.
			return framesSincePlayerSeen <= SfxSightLeniency
				&& direction == lastCrushDir;
		}

		public CrusherKevin(Vector2 position, float width, float height, bool right, bool up, bool left, bool down, bool chillOut = false, bool interruptable = false, bool nonReturn = true)
			: base(position, width, height, safe: false)
		{
			returnStack = new List<MoveState>();
            this.canMoveRight = right;
            this.canMoveUp = up;
            this.canMoveLeft = left;
            this.canMoveDown = down;
			this.chillOut = chillOut;
			this.interruptable = interruptable;
			this.nonReturn = nonReturn;
			giant = base.Width >= 48f && base.Height >= 48f && chillOut;
			coolingDown = false;
			canActivate = true;
			attackCoroutine = new Coroutine();
			attackCoroutine.RemoveOnComplete = false;
			Add(attackCoroutine);
			List<MTexture> atlasSubtextures = GFX.Game.GetAtlasSubtextures("objects/ShaojiaHelper/crusherKevin/block");
			MTexture idle;
            int bitmask = (right ? 1 : 0) | (up ? 2 : 0) | (left ? 4 : 0) | (down ? 8 : 0);
			idle = atlasSubtextures[bitmask];
			Add(face = GFX.SpriteBank.Create(giant ? "giant_crushblock_face" : "crushblock_face"));
			face.Position = new Vector2(base.Width, base.Height) / 2f;
			face.Play("idle");
			face.OnLastFrame = delegate (string f)
			{
				if (f == "hit")
				{
					face.Play(nextFaceDirection);
				}
			};
			int num = (int)(base.Width / 8f) - 1;
			int num2 = (int)(base.Height / 8f) - 1;
			AddImage(idle, 0, 0, 0, 0, -1, -1);
			AddImage(idle, num, 0, 3, 0, 1, -1);
			AddImage(idle, 0, num2, 0, 3, -1, 1);
			AddImage(idle, num, num2, 3, 3, 1, 1);
			for (int i = 1; i < num; i++)
			{
				AddImage(idle, i, 0, Calc.Random.Choose(1, 2), 0, 0, -1);
				AddImage(idle, i, num2, Calc.Random.Choose(1, 2), 3, 0, 1);
			}
			for (int j = 1; j < num2; j++)
			{
				AddImage(idle, 0, j, 0, Calc.Random.Choose(1, 2), -1);
				AddImage(idle, num, j, 3, Calc.Random.Choose(1, 2), 1);
			}
			Add(new LightOcclude(0.2f));
			Add(returnLoopSfx = new SoundSource());
			Add(new TransitionListener
			{
				OnOutBegin = delegate
				{
					StopMoveLoopSfx(allowFadeOut: true);
				}
			});
			Add(new WaterInteraction(() => crushDir != Vector2.Zero));
		}

		public CrusherKevin(EntityData data, Vector2 offset)
			: this(data.Position + offset, data.Width, data.Height, data.Bool("lookRight"), data.Bool("lookUp"), data.Bool("lookLeft"), data.Bool("lookDown"), data.Bool("chillout"), data.Bool("interruptable"), data.Bool("nonReturn"))
		{
			requiresLineOfSight = data.Bool("requiresLineOfSight");
		}

		public override void Added(Scene scene)
		{
			base.Added(scene);
			level = SceneAs<Level>();
		}

		public override void Removed(Scene scene)
		{
			StopMoveLoopSfx(allowFadeOut: true);
			base.Removed(scene);
		}

		public override void SceneEnd(Scene scene)
		{
			StopMoveLoopSfx(allowFadeOut: false);
			base.SceneEnd(scene);
		}

		// Some code for this function is taken from CommunalHelper, written by Catapillie
		// https://github.com/CommunalHelper/CommunalHelper/blob/dev/src/Entities/Misc/Melvin.cs
		private bool IsPlayerSeen(Player player, Vector2 dir)
		{
			Rectangle rect = new Rectangle();
			int y1 = (int)Math.Max(player.Top, Top);
			int y2 = (int)Math.Min(player.Bottom, Bottom);
			int x1 = (int)Math.Max(player.Left, Left);
			int x2 = (int)Math.Min(player.Right, Right);
			if (dir.X == 1 && dir.Y == 0)
            {
				// right
				rect = new Rectangle((int)(X + Width), y1, (int)(player.Left - X - Width), y2 - y1);
			} 
			else if (dir.X == -1 && dir.Y == 0)
            {
				// left
				rect = new Rectangle((int)player.Right, y1, (int)(X - player.Right), y2 - y1);
			}
			else if (dir.X == 0 && dir.Y == 1)
            {
				// down
				rect = new Rectangle(x1, (int)(Y + Height), x2 - x1, (int)(player.Top - Y - Height));
			}
			else if (dir.X == 0 && dir.Y == -1)
            {
				// up
				rect = new Rectangle(x1, (int)player.Bottom, x2 - x1, (int)(Y - player.Bottom));
			}

			if (dir.Y != 0)
			{
				for (int i = 0; i < rect.Width; i++)
				{
					Rectangle lineRect = new(rect.X + i, rect.Y, 1, rect.Height);
					if (!Scene.CollideCheck<Solid>(lineRect))
						return true;
				}
				return false;
			}
			else
			{
				for (int i = 0; i < rect.Height; i++)
				{
					Rectangle lineRect = new(rect.X, rect.Y + i, rect.Width, 1);
					if (!Scene.CollideCheck<Solid>(lineRect))
						return true;
				}
				return false;
			}
		}

		public override void Update()
		{
			base.Update();
			if (crushDir == Vector2.Zero)
			{
				face.Position = new Vector2(base.Width, base.Height) / 2f;
				if (CollideCheck<Player>(Position + new Vector2(-1f, 0f)))
				{
					face.X -= 1f;
				}
				else if (CollideCheck<Player>(Position + new Vector2(1f, 0f)))
				{
					face.X += 1f;
				}
				else if (CollideCheck<Player>(Position + new Vector2(0f, -1f)))
				{
					face.Y -= 1f;
				}
			}
			if (currentMoveLoopSfx != null)
			{
				if (currentMoveLoopSfxIs3D)
				{
					Audio.Position(currentMoveLoopSfx, Center);
				}
				Audio.SetParameter(currentMoveLoopSfx, "submerged", Submerged ? 1f : 0f);
			}
			if (returnLoopSfx != null)
			{
				returnLoopSfx.Param("submerged", Submerged ? 1 : 0);
			}

			if (!coolingDown){
				framesSinceLastAttack++;
			}
			bool attacked = false;
			bool playerSeenThisFrame = false;
			foreach (Player player in base.Scene.Tracker.GetEntities<Player>())
			{
				Vector2 dir;
				int up = (int)(Position.Y - player.Bottom);
				int down = (int)(player.Top - Position.Y - Height);
				int left = (int)(Position.X - player.Right);
				int right = (int)(player.Left - Position.X - Width);
				if (up>=0 && left<0 && right<0)
				{
					dir.X=0;
					dir.Y=-1;
				}
				else if (down>=0 && left<0 && right<0)
				{
					dir.X=0;
					dir.Y=1;
				}
				else if (left>=0 && up<0 && down<0)
				{
					dir.X=-1;
					dir.Y=0;
				}
				else if (right>=0 && up<0 && down<0)
				{
					dir.X=1;
					dir.Y=0;
				}
				else
				{
					continue;
				}
				// Engine.Commands.Log($"[Debug] U:{up} D:{down} L:{left} R:{right}");
				if (requiresLineOfSight && !IsPlayerSeen(player, dir))
				{
					continue;
				}
				playerSeenThisFrame = true;
				if (!CanActivate(dir))
				{
					continue;
				}
				Attack(dir);
				attacked = true;
			}
			// Keep the pre-frame value while Attack() runs so condition 2 sees how
			// many consecutive frames Kevin had temporarily lost sight of a player.
			if (playerSeenThisFrame)
			{
				framesSincePlayerSeen = 0;
			}
			else if (framesSincePlayerSeen < int.MaxValue)
			{
				framesSincePlayerSeen++;
			}
			if (framesSinceLastAttack >= SfxIntervalLeniency && !attacked && crushDir == Vector2.Zero && currentMoveLoopSfx != null)
			{
				EndMoveLoopSfx(currentMoveLoopSfx);
				currentMoveLoopSfx = null;
			}
		}

		public override void Render()
		{
			Vector2 position = Position;
			Position += base.Shake;
			Draw.Rect(base.X + 2f, base.Y + 2f, base.Width - 4f, base.Height - 4f, fill);
			base.Render();
			Position = position;
		}

		private void AddImage(MTexture idle, int x, int y, int tx, int ty, int borderX = 0, int borderY = 0)
		{
			MTexture subtexture = idle.GetSubtexture(tx * 8, ty * 8, 8, 8);
			Vector2 vector = new Vector2(x * 8, y * 8);
			if (borderX != 0)
			{
				Image image = new Image(subtexture);
				image.Color = Color.Black;
				image.Position = vector + new Vector2(borderX, 0f);
				Add(image);
			}
			if (borderY != 0)
			{
				Image image2 = new Image(subtexture);
				image2.Color = Color.Black;
				image2.Position = vector + new Vector2(0f, borderY);
				Add(image2);
			}
			Image image3 = new Image(subtexture);
			image3.Position = vector;
			Add(image3);
			idleImages.Add(image3);
			if (borderX != 0 || borderY != 0)
			{
				if (borderX < 0)
				{
					Image image4 = new Image(GFX.Game["objects/crushblock/lit_left"].GetSubtexture(0, ty * 8, 8, 8));
					activeLeftImages.Add(image4);
					image4.Position = vector;
					image4.Visible = false;
					Add(image4);
				}
				else if (borderX > 0)
				{
					Image image5 = new Image(GFX.Game["objects/crushblock/lit_right"].GetSubtexture(0, ty * 8, 8, 8));
					activeRightImages.Add(image5);
					image5.Position = vector;
					image5.Visible = false;
					Add(image5);
				}
				if (borderY < 0)
				{
					Image image6 = new Image(GFX.Game["objects/crushblock/lit_top"].GetSubtexture(tx * 8, 0, 8, 8));
					activeTopImages.Add(image6);
					image6.Position = vector;
					image6.Visible = false;
					Add(image6);
				}
				else if (borderY > 0)
				{
					Image image7 = new Image(GFX.Game["objects/crushblock/lit_bottom"].GetSubtexture(tx * 8, 0, 8, 8));
					activeBottomImages.Add(image7);
					image7.Position = vector;
					image7.Visible = false;
					Add(image7);
				}
			}
		}

		private void TurnOffImages()
		{
			foreach (Image activeLeftImage in activeLeftImages)
			{
				activeLeftImage.Visible = false;
			}
			foreach (Image activeRightImage in activeRightImages)
			{
				activeRightImage.Visible = false;
			}
			foreach (Image activeTopImage in activeTopImages)
			{
				activeTopImage.Visible = false;
			}
			foreach (Image activeBottomImage in activeBottomImages)
			{
				activeBottomImage.Visible = false;
			}
		}

		private bool CanActivate(Vector2 direction)
		{
			if (giant && direction.X <= 0f)
			{
				return false;
			}
            if (canActivate && crushDir != direction)
            {
                if (direction.X > 0f && !canMoveRight) return false;
                if (direction.X < 0f && !canMoveLeft)  return false;
                if (direction.Y > 0f && !canMoveDown)  return false;
                if (direction.Y < 0f && !canMoveUp)    return false;

                return true;
            }
			return false;
		}

		private void Attack(Vector2 direction)
		{
			// A new attack replaces the return coroutine, so stop its loop here;
			// the coroutine's own Stop() call will never run when interrupted.
			returnLoopSfx.Stop();
			if (!ShouldSuppressAttackSfx(direction))
			{
				PlayOneShotSfx("event:/game/06_reflection/crushblock_activate", base.Center, EnableAttackSfx);
			}
			if (EnableMoveLoopSfx && (framesSinceLastAttack > SfxIntervalLeniency || currentMoveLoopSfx == null))
			{
				if (currentMoveLoopSfx != null)
				{
					EndMoveLoopSfx(currentMoveLoopSfx);
					currentMoveLoopSfx = null;
				}

				string moveLoopSfxPath = "event:/game/06_reflection/crushblock_move_loop";
				if (SaveData.Instance != null && SaveData.Instance.Name != null && SaveData.Instance.Name.StartsWith("FWAHAHA", StringComparison.InvariantCultureIgnoreCase))
				{
					moveLoopSfxPath = "event:/game/06_reflection/crushblock_move_loop_covert";
				}
				currentMoveLoopSfx = PlayMoveLoopSfx(moveLoopSfxPath);
			}
			face.Play("hit");
			crushDir = direction;
			lastCrushDir = direction;
			canActivate = false;
			attackCoroutine.Replace(AttackSequence());
			ClearRemainder();
			TurnOffImages();
			ActivateParticles(crushDir);
			if (crushDir.X < 0f)
			{
				foreach (Image activeLeftImage in activeLeftImages)
				{
					activeLeftImage.Visible = true;
				}
				nextFaceDirection = "left";
			}
			else if (crushDir.X > 0f)
			{
				foreach (Image activeRightImage in activeRightImages)
				{
					activeRightImage.Visible = true;
				}
				nextFaceDirection = "right";
			}
			else if (crushDir.Y < 0f)
			{
				foreach (Image activeTopImage in activeTopImages)
				{
					activeTopImage.Visible = true;
				}
				nextFaceDirection = "up";
			}
			else if (crushDir.Y > 0f)
			{
				foreach (Image activeBottomImage in activeBottomImages)
				{
					activeBottomImage.Visible = true;
				}
				nextFaceDirection = "down";
			}
			bool flag = true;
			if (returnStack.Count > 0)
			{
				MoveState moveState = returnStack[returnStack.Count - 1];
				if (moveState.Direction == direction || moveState.Direction == -direction)
				{
					flag = false;
				}
			}
			if (flag)
			{
				returnStack.Add(new MoveState(Position, crushDir));
			}
		}

		private void ActivateParticles(Vector2 dir)
		{
			float direction;
			Vector2 position;
			Vector2 positionRange;
			int num;
			if (dir == Vector2.UnitX)
			{
				direction = 0f;
				position = base.CenterRight - Vector2.UnitX;
				positionRange = Vector2.UnitY * (base.Height - 2f) * 0.5f;
				num = (int)(base.Height / 8f) * 4;
			}
			else if (dir == -Vector2.UnitX)
			{
				direction = (float)Math.PI;
				position = base.CenterLeft + Vector2.UnitX;
				positionRange = Vector2.UnitY * (base.Height - 2f) * 0.5f;
				num = (int)(base.Height / 8f) * 4;
			}
			else if (dir == Vector2.UnitY)
			{
				direction = (float)Math.PI / 2f;
				position = base.BottomCenter - Vector2.UnitY;
				positionRange = Vector2.UnitX * (base.Width - 2f) * 0.5f;
				num = (int)(base.Width / 8f) * 4;
			}
			else
			{
				direction = -(float)Math.PI / 2f;
				position = base.TopCenter + Vector2.UnitY;
				positionRange = Vector2.UnitX * (base.Width - 2f) * 0.5f;
				num = (int)(base.Width / 8f) * 4;
			}
			num += 2;
			level.Particles.Emit(CrushBlock.P_Activate, num, position, positionRange, direction);
		}

		private IEnumerator AttackSequence()
		{
			// Snapshot the position at attack time so Impact can be skipped when
			// Kevin never actually moved along this crush direction.
			Vector2 attackStartPosition = Position;
			Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
			StartShaking(0.4f);
			yield return 0.4f;
			framesSinceLastAttack = 0;
			if (!chillOut && interruptable)
			{
				canActivate = true;
			}
			StopPlayerRunIntoAnimation = false;
			bool slowing = false;
			float speed = 0f;
			while (true)
			{
				if (!chillOut)
				{
					speed = Calc.Approach(speed, CrushSpeed, CrushAccel * Engine.DeltaTime);
				}
				else if (slowing || CollideCheck<SolidTiles>(Position + crushDir * 256f))
				{
					speed = Calc.Approach(speed, 24f, CrushAccel * Engine.DeltaTime * 0.25f);
					if (!slowing)
					{
						slowing = true;
						Alarm.Set(this, 0.5f, delegate
						{
							face.Play("hurt");
							TurnOffImages();
						});
					}
				}
				else
				{
					speed = Calc.Approach(speed, CrushSpeed, CrushAccel * Engine.DeltaTime);
				}
				bool flag = ((crushDir.X == 0f) ? MoveVCheck(speed * crushDir.Y * Engine.DeltaTime) : MoveHCheck(speed * crushDir.X * Engine.DeltaTime));
				if (Top >= (float)(level.Bounds.Bottom + 32))
				{
					RemoveSelf();
					yield break;
				}
				if (flag)
				{
					break;
				}
				if (Scene.OnInterval(0.02f))
				{
					Vector2 position;
					float direction;
					if (crushDir == Vector2.UnitX)
					{
						position = new Vector2(Left + 1f, Calc.Random.Range(Top + 3f, Bottom - 3f));
						direction = (float)Math.PI;
					}
					else if (crushDir == -Vector2.UnitX)
					{
						position = new Vector2(Right - 1f, Calc.Random.Range(Top + 3f, Bottom - 3f));
						direction = 0f;
					}
					else if (crushDir == Vector2.UnitY)
					{
						position = new Vector2(Calc.Random.Range(Left + 3f, Right - 3f), Top + 1f);
						direction = -(float)Math.PI / 2f;
					}
					else
					{
						position = new Vector2(Calc.Random.Range(Left + 3f, Right - 3f), Bottom - 1f);
						direction = (float)Math.PI / 2f;
					}
					level.Particles.Emit(CrushBlock.P_Crushing, position, direction);
				}
				yield return null;
			}
			FallingBlock fallingBlock = CollideFirst<FallingBlock>(Position + crushDir);
			if (fallingBlock != null)
			{
				fallingBlock.Triggered = true;
			}
			if (crushDir == -Vector2.UnitX)
			{
				Vector2 vector = new Vector2(0f, 2f);
				for (int i = 0; (float)i < Height / 8f; i++)
				{
					Vector2 vector2 = new Vector2(Left - 1f, Top + 4f + (float)(i * 8));
					if (!Scene.CollideCheck<Water>(vector2) && Scene.CollideCheck<Solid>(vector2))
					{
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector2 + vector, 0f);
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector2 - vector, 0f);
					}
				}
			}
			else if (crushDir == Vector2.UnitX)
			{
				Vector2 vector3 = new Vector2(0f, 2f);
				for (int j = 0; (float)j < Height / 8f; j++)
				{
					Vector2 vector4 = new Vector2(Right + 1f, Top + 4f + (float)(j * 8));
					if (!Scene.CollideCheck<Water>(vector4) && Scene.CollideCheck<Solid>(vector4))
					{
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector4 + vector3, (float)Math.PI);
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector4 - vector3, (float)Math.PI);
					}
				}
			}
			else if (crushDir == -Vector2.UnitY)
			{
				Vector2 vector5 = new Vector2(2f, 0f);
				for (int k = 0; (float)k < Width / 8f; k++)
				{
					Vector2 vector6 = new Vector2(Left + 4f + (float)(k * 8), Top - 1f);
					if (!Scene.CollideCheck<Water>(vector6) && Scene.CollideCheck<Solid>(vector6))
					{
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector6 + vector5, (float)Math.PI / 2f);
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector6 - vector5, (float)Math.PI / 2f);
					}
				}
			}
			else if (crushDir == Vector2.UnitY)
			{
				Vector2 vector7 = new Vector2(2f, 0f);
				for (int l = 0; (float)l < Width / 8f; l++)
				{
					Vector2 vector8 = new Vector2(Left + 4f + (float)(l * 8), Bottom + 1f);
					if (!Scene.CollideCheck<Water>(vector8) && Scene.CollideCheck<Solid>(vector8))
					{
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector8 + vector7, -(float)Math.PI / 2f);
						SceneAs<Level>().ParticlesFG.Emit(CrushBlock.P_Impact, vector8 - vector7, -(float)Math.PI / 2f);
					}
				}
			}
			coolingDown = true;
			// Non-chillout speed only increases, so the speed on the collision frame
			// is this attack's maximum. Chillout always returns full volume.
			float impactVolume = GetImpactSfxVolume(speed);
			bool movedSinceAttack = HasMovedSinceAttack(attackStartPosition);
			bool playImpact = movedSinceAttack && impactVolume >= ImpactVolumeThreshold;
			if (playImpact)
			{
				PlayImpactSfx(Center, impactVolume);
			}
			level.DirectionalShake(crushDir);
			Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
			StartShaking(0.4f);
			StopPlayerRunIntoAnimation = true;
			crushDir = Vector2.Zero;
			TurnOffImages();
			coolingDown = false;
			if (chillOut)
			{
				yield break;
            }
			canActivate = true;
			if (nonReturn)
			{
				face.Play("idle");
				// Not returnable: R and return_waypoint are canceled.
				yield break;
			}
			face.Play("hurt");
			if (EnableReturnLoopSfx)
			{
				returnLoopSfx.Play("event:/game/06_reflection/crushblock_return_loop");
			}
			yield return 0.4f;
			speed = 0f;
			float waypointSfxDelay = 0f;
			while (returnStack.Count > 0)
			{
				yield return null;
				StopPlayerRunIntoAnimation = false;
				MoveState moveState = returnStack[returnStack.Count - 1];
				speed = Calc.Approach(speed, ReturnSpeed, ReturnAccel * Engine.DeltaTime);
				waypointSfxDelay -= Engine.DeltaTime;
				if (moveState.Direction.X != 0f)
				{
					MoveTowardsX(moveState.From.X, speed * Engine.DeltaTime);
				}
				if (moveState.Direction.Y != 0f)
				{
					MoveTowardsY(moveState.From.Y, speed * Engine.DeltaTime);
				}
				if ((moveState.Direction.X != 0f && ExactPosition.X != moveState.From.X) || (moveState.Direction.Y != 0f && ExactPosition.Y != moveState.From.Y))
				{
					continue;
				}
				speed = 0f;
				returnStack.RemoveAt(returnStack.Count - 1);
				StopPlayerRunIntoAnimation = true;
				if (returnStack.Count <= 0)
				{
					face.Play("idle");
					returnLoopSfx.Stop();
					if (waypointSfxDelay <= 0f)
					{
						PlayOneShotSfx("event:/game/06_reflection/crushblock_rest", Center, EnableRestSfx && Returnable);
					}
				}
				else if (waypointSfxDelay <= 0f)
				{
					PlayOneShotSfx("event:/game/06_reflection/crushblock_rest_waypoint", Center, EnableRestWaypointSfx && Returnable);
				}
				waypointSfxDelay = 0.1f;
				StartShaking(0.2f);
				yield return 0.2f;
			}
		}

		private bool MoveHCheck(float amount)
		{
			if (MoveHCollideSolidsAndBounds(level, amount, thruDashBlocks: true))
			{
				if (amount < 0f && base.Left <= (float)level.Bounds.Left)
				{
					return true;
				}
				if (amount > 0f && base.Right >= (float)level.Bounds.Right)
				{
					return true;
				}
				for (int i = 1; i <= 4; i++)
				{
					for (int num = 1; num >= -1; num -= 2)
					{
						Vector2 vector = new Vector2(Math.Sign(amount), i * num);
						if (!CollideCheck<Solid>(Position + vector))
						{
							MoveVExact(i * num);
							MoveHExact(Math.Sign(amount));
							return false;
						}
					}
				}
				return true;
			}
			return false;
		}

		private bool MoveVCheck(float amount)
		{
			if (MoveVCollideSolidsAndBounds(level, amount, thruDashBlocks: true, null, checkBottom: false))
			{
				if (amount < 0f && base.Top <= (float)level.Bounds.Top)
				{
					return true;
				}
				for (int i = 1; i <= 4; i++)
				{
					for (int num = 1; num >= -1; num -= 2)
					{
						Vector2 vector = new Vector2(i * num, Math.Sign(amount));
						if (!CollideCheck<Solid>(Position + vector))
						{
							MoveHExact(i * num);
							MoveVExact(Math.Sign(amount));
							return false;
						}
					}
				}
				return true;
			}
			return false;
		}

    }
}