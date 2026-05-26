using System;
using System.Collections;
using System.Collections.Generic;
using Celeste.Mod.Entities;
using Microsoft.Xna.Framework;
using Monocle;

namespace Celeste.Mod.KevinCrusher.Entities {

    [CustomEntity("KevinCrusher/KevinCrusher")]
    [TrackedAs(typeof(CrushBlock))]
    public class KevinCrusher : Solid {
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

		private Color fill = Calc.HexToColor("62222b");

		private Level level;

		private Vector2 crushDir;

		private List<MoveState> returnStack;

		private Coroutine attackCoroutine;

		private bool canActivate;
		private bool canMoveUp;
		private bool canMoveDown;
		private bool canMoveLeft;
		private bool canMoveRight;
		private bool chillOut;
		private bool giant;
		private bool requiresLineOfSight;

		private Sprite face;

		private string nextFaceDirection;

		private List<Image> idleImages = new List<Image>();
		private List<Image> activeTopImages = new List<Image>();
		private List<Image> activeRightImages = new List<Image>();
		private List<Image> activeLeftImages = new List<Image>();
		private List<Image> activeBottomImages = new List<Image>();

		private SoundSource currentMoveLoopSfx;
		private SoundSource returnLoopSfx;

		private bool Submerged => base.Scene.CollideCheck<Water>(new Rectangle((int)(base.Center.X - 4f), (int)base.Center.Y, 8, 4));

		public KevinCrusher(Vector2 position, float width, float height, bool right, bool up, bool left, bool down, bool chillOut = false)
			: base(position, width, height, safe: false)
		{
			returnStack = new List<MoveState>();
            this.canMoveRight = right;
            this.canMoveUp = up;
            this.canMoveLeft = left;
            this.canMoveDown = down;
			this.chillOut = chillOut;
			giant = base.Width >= 48f && base.Height >= 48f && chillOut;
			canActivate = true;
			attackCoroutine = new Coroutine();
			attackCoroutine.RemoveOnComplete = false;
			Add(attackCoroutine);
			List<MTexture> atlasSubtextures = GFX.Game.GetAtlasSubtextures("objects/KevinCrusher/Shaojia07/KevinCrusher/block");
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
			Add(new WaterInteraction(() => crushDir != Vector2.Zero));
		}

		public KevinCrusher(EntityData data, Vector2 offset)
			: this(data.Position + offset, data.Width, data.Height, data.Bool("lookRight"), data.Bool("lookUp"), data.Bool("lookLeft"), data.Bool("lookDown"), data.Bool("chillout"))
		{
			requiresLineOfSight = data.Bool("requiresLineOfSight");
		}

		public override void Added(Scene scene)
		{
			base.Added(scene);
			level = SceneAs<Level>();
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
				currentMoveLoopSfx.Param("submerged", Submerged ? 1 : 0);
			}
			if (returnLoopSfx != null)
			{
				returnLoopSfx.Param("submerged", Submerged ? 1 : 0);
			}

            if (canActivate)
            {
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
                    if (!CanActivate(dir))
                    {
                        continue;
                    }
                    if (requiresLineOfSight && !IsPlayerSeen(player, dir))
                    {
                        continue;
                    }
                    Attack(dir);
                }    
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
			Audio.Play("event:/game/06_reflection/crushblock_activate", base.Center);
			if (currentMoveLoopSfx != null)
			{
				currentMoveLoopSfx.Param("end", 1f);
				SoundSource sfx = currentMoveLoopSfx;
				Alarm.Set(this, 0.5f, delegate
				{
					sfx.RemoveSelf();
				});
			}
			Add(currentMoveLoopSfx = new SoundSource());
			currentMoveLoopSfx.Position = new Vector2(base.Width, base.Height) / 2f;
			if (SaveData.Instance != null && SaveData.Instance.Name != null && SaveData.Instance.Name.StartsWith("FWAHAHA", StringComparison.InvariantCultureIgnoreCase))
			{
				currentMoveLoopSfx.Play("event:/game/06_reflection/crushblock_move_loop_covert");
			}
			else
			{
				currentMoveLoopSfx.Play("event:/game/06_reflection/crushblock_move_loop");
			}
			face.Play("hit");
			crushDir = direction;
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
			Input.Rumble(RumbleStrength.Strong, RumbleLength.Medium);
			StartShaking(0.4f);
			yield return 0.4f;
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
							SoundSource soundSource = currentMoveLoopSfx;
							if (soundSource != null)
							{
								soundSource = soundSource.Stop();
							}
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
			Audio.Play("event:/game/06_reflection/crushblock_impact", Center);
			level.DirectionalShake(crushDir);
			Input.Rumble(RumbleStrength.Medium, RumbleLength.Medium);
			StartShaking(0.4f);
			StopPlayerRunIntoAnimation = true;
			SoundSource sfx = currentMoveLoopSfx;
			currentMoveLoopSfx.Param("end", 1f);
			currentMoveLoopSfx = null;
			Alarm.Set(this, 0.5f, delegate
			{
				sfx.RemoveSelf();
			});
			crushDir = Vector2.Zero;
			TurnOffImages();
			if (chillOut)
			{
				yield break;
            }
			canActivate = true;
			face.Play("hurt");
			returnLoopSfx.Play("event:/game/06_reflection/crushblock_return_loop");
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
						Audio.Play("event:/game/06_reflection/crushblock_rest", Center);
					}
				}
				else if (waypointSfxDelay <= 0f)
				{
					Audio.Play("event:/game/06_reflection/crushblock_rest_waypoint", Center);
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