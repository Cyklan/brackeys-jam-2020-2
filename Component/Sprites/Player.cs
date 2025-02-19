﻿using brackeys_2020_2_jam.Component.Sprites.Environment;
using brackeys_2020_2_jam.Component.Sprites.Obstacles;
using brackeys_2020_2_jam.Manager;
using brackeys_2020_2_jam.Models;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.CompilerServices;

namespace brackeys_2020_2_jam.Component.Sprites
{
    public class Player : Sprite
    {
        private KeyboardState CurrentKeyboard { get; set; }
        private KeyboardState PreviousKeyboard { get; set; }

        public const float FALL_MULTIPLIER = 0.5f;
        public const float JUMP_VELOCITY = -35f;

        public float ConveyorSpeed { get; set; }

        public const float ALIVE_CHARGE = 200f;
        public const float ALIVE_MAX = 1000f;

        public float AliveDrain { get; set; } = 1f;

        public const double WINDUP_TIME_IN_SECONDS = 1;

        public float AliveTimer { get; set; }
        public double CurrentWindupTime { get; set; }
        public double IFrames { get; } = 2;
        public double IFramesTimer { get; set; }
        public bool CanTakeDamage => IFramesTimer > IFrames;

        public bool IsInAir => Speed.Y != 0;
        public bool IsWindingUp { get; set; }
        public PlayerInput Input { get; set; }

        public bool IsGoingLeft => Speed.X < 0;
        public bool IsGoingRight => Speed.X > 0;
        public bool IsStandingStill => Speed.X == 0;
        public bool IsOnConveyor { get; private set; }
        public bool IsJumping { get; private set; }

        public float Acceleration { get; set; }
        public float CurrentAcceleration { get; private set; }
        public float MaxAcceleration { get; set; }

        public float MaxFallAcceleration => 0.3f;
        public float FallAcceleration { get; set; }

        private Dictionary<string, Animation> Animations { get; set; }
        private SoundEffectInstance WalkSoundEffect { get; set; }
        private double WalkSoundTimer { get; set; }
        private double WalkSoundIntervall => 0.25;

        private readonly AnimationManager WindUpAnimationManager;

        private SoundEffectInstance WindDownSoundEffect { get; set; }
        private double WindDownSoundIntervall => 0.2;
        private double WindDownSoundIntervallTimer { get; set; }

        public Player(PlayerInput input)
        {
            WindUpAnimationManager = new AnimationManager
            {
                Scale = 0.4f,
                FlipVertically = true
            };

            WindDownSoundEffect = ContentManager.WinddownSoundEffect.CreateInstance();
            WindDownSoundEffect.Volume = 0.25f;

            AliveTimer = 0;
            Input = input;
            Texture = ContentManager.StandingAnimation;
            Speed = new Vector2(0, -1);
            IsOnConveyor = false;

            Animations = new Dictionary<string, Animation>
            {
                { "walk", new Animation(ContentManager.WalkingAnimation, 29) {  FrameSpeed = 0.01f} },
                { "standing", new Animation(ContentManager.StandingAnimation, 1) }
            };
            AnimationManager.Scale = 0.2f;
            AnimationManager.Parent = this;
            AnimationManager.Play(Animations["standing"]);
            Size = new Size(AnimationManager.AnimationRectangle.Size.X, AnimationManager.AnimationRectangle.Size.Y);

            WalkSoundEffect = ContentManager.StepSoundEffect.CreateInstance();

            IFramesTimer = IFrames;
        }

        public override void OnCollision(Sprite sprite, GameTime gameTime)
        {
            if (sprite is null) return;
            if (sprite is Clock) return;
            if (sprite is Conveyor) return;

            if (sprite is StickyObstacle && CanTakeDamage) return;

            if (IsTouchingRight(sprite))
            {
                if (!IsJumping) AnimationManager.Play(Animations["standing"]);
                Speed = new Vector2(0, Speed.Y);
                Position = new Vector2(sprite.Hitbox.X + sprite.Hitbox.Width, Position.Y);
            }
            else if (IsTouchingLeft(sprite))
            {
                if (!IsJumping) AnimationManager.Play(Animations["standing"]);
                Speed = new Vector2(0, Speed.Y);
                Position = new Vector2(sprite.Hitbox.X - Rectangle.Width, Position.Y);
            }
            else if (IsTouchingTop(sprite))
            {
                Speed = new Vector2(Speed.X, 0);
                Position = new Vector2(Position.X, sprite.Hitbox.Y - HitBoxYOffSet - Hitbox.Height);
                FallAcceleration = 0;
                IsOnConveyor = true;
                if (sprite is Chopper) IsRemoved = true;
                if (IsJumping)
                {
                    JumpLandingAnimation();
                    AudioManager.PlayEffect(ContentManager.LandSoundEffect);
                    IsJumping = false;
                }
            }

        }

        public override void Update(GameTime gameTime)
        {
            IFramesTimer += gameTime.ElapsedGameTime.TotalSeconds;
            WalkSoundTimer += gameTime.ElapsedGameTime.TotalSeconds;
            WindDownSoundIntervallTimer += gameTime.ElapsedGameTime.TotalSeconds;

            if (IsStandingStill & !IsInAir && !IsJumping) AnimationManager.Play(Animations["standing"]);

            PreviousKeyboard = CurrentKeyboard;
            CurrentKeyboard = Keyboard.GetState();

            if (AliveTimer > 0 && !IsWindingUp) AliveTimer -= AliveDrain;

            if (IsInAir) IsOnConveyor = false;

            Windup(gameTime);
            FallDown();
            Move();
            MoveParticle();
            PlayWindDownSoundEffect();

            Position += Speed;
            if (IsOnConveyor) Position = new Vector2(Position.X - ConveyorSpeed, Position.Y);

            if (WindUpAnimationManager.IsPlaying) WindUpAnimationManager.Update(gameTime);
            base.Update(gameTime);
        }

        public override void Draw(GameTime gameTime, SpriteBatch spriteBatch)
        {
            WindUpAnimation();
            if (WindUpAnimationManager.IsPlaying) WindUpAnimationManager.Draw(spriteBatch);

            base.Draw(gameTime, spriteBatch);
        }

        private void MoveParticle()
        {
            if (IsInAir || IsStandingStill) return;

            ParticleManager.Textures = ContentManager.DustParticles;
            if (IsGoingLeft)
            {
                ParticleManager.EmitterLocation = new Vector2(Position.X + Rectangle.Width - 15, Position.Y + Rectangle.Height - 5);
            }
            else if (IsGoingRight)
            {
                ParticleManager.EmitterLocation = new Vector2(Position.X + 15, Position.Y + Rectangle.Height - 5);
            }

            if (WalkSoundEffect.State == SoundState.Stopped && WalkSoundTimer > WalkSoundIntervall)
            {
                WalkSoundEffect.Play();
                WalkSoundTimer = 0;
            }

            ParticleManager.GenerateNewParticle(Microsoft.Xna.Framework.Color.White, 3, 2);
        }

        private void FallDown()
        {
            Speed += Vector2.UnitY * GRAVITY.Y * FallAcceleration;

            if (FallAcceleration < MaxFallAcceleration)
                FallAcceleration += FALL_MULTIPLIER;
            else
                FallAcceleration = MaxFallAcceleration;

            if (Speed.Y > TERMINAL_VELOCITY) Speed = new Vector2(Speed.X, TERMINAL_VELOCITY);
        }

        private void PlayWindDownSoundEffect()
        {
            if (AliveTimer == 0 || IsWindingUp)
            {
                WindDownSoundEffect.Stop();
            }
            else
            {
                if ( WindDownSoundIntervallTimer > WindDownSoundIntervall)
                {
                    if (WindDownSoundEffect.State == SoundState.Stopped)
                        WindDownSoundEffect.Play();

                    WindDownSoundIntervallTimer = 0;
                }
            }
        }

        private void WindUpAnimation()
        {
            if (AliveTimer == 0)
                WindUpAnimationManager.Pause();
            else
                WindUpAnimationManager.Continue();

            if (WindUpAnimationManager.Flip)
            {
                if (IsStandingStill)
                    WindUpAnimationManager.Position = new Vector2(Position.X - Rectangle.Width, Position.Y + (Rectangle.Height / 7));
                else
                    WindUpAnimationManager.Position = new Vector2(Position.X - Rectangle.Width, Position.Y + (Rectangle.Height / 7));
            }
            else
            {
                if (IsStandingStill)
                    WindUpAnimationManager.Position = new Vector2(Position.X + Rectangle.Width - 20, Position.Y + (Rectangle.Height / 7));
                else
                    WindUpAnimationManager.Position = new Vector2(Position.X + Rectangle.Width - 10, Position.Y + (Rectangle.Height / 7));
            }

            if (!WindUpAnimationManager.IsPlaying) WindUpAnimationManager.Play(new Animation(ContentManager.WindupAnimation, 5) { FrameSpeed = 0.2f });
        }

        private void Windup(GameTime gameTime)
        {

            if (Speed.X != 0 || Speed.Y != 0 || IsInAir)
            {
                IsWindingUp = false;
                return;
            }

            if (Keyboard.GetState().IsKeyDown(Input.Windup))
            {
                if (!IsWindingUp)
                {
                    WindUpAnimationManager.Reverse = true;
                }

                IsWindingUp = true;
                if (CurrentWindupTime > WINDUP_TIME_IN_SECONDS)
                {
                    AudioManager.PlayEffect(ContentManager.WindupSoundEffect, 0.5f);
                    AliveTimer += ALIVE_CHARGE;
                    if (AliveTimer > ALIVE_MAX) AliveTimer = ALIVE_MAX;
                    CurrentWindupTime = 0;
                }

                CurrentWindupTime += gameTime.ElapsedGameTime.TotalSeconds;

            }
            else { IsWindingUp = false; WindUpAnimationManager.Reverse = false; }
        }

        private void Move()
        {
            CheckMove();
            CheckJump();
        }

        private void CheckMove()
        {
            if (Keyboard.GetState().IsKeyDown(Input.Right) && AliveTimer > 0 && !IsWindingUp)
            {
                AnimationManager.Flip = true;
                WindUpAnimationManager.Flip = true;
                if (!IsInAir && !IsJumping) AnimationManager.Play(Animations["walk"]);

                if (IsGoingLeft)
                {
                    Break();
                    return;
                }

                Accelerate();

                if (Speed.X < MaxSpeed.X)
                    Speed = new Vector2(Speed.X + CurrentAcceleration, Speed.Y);
                else
                    Speed = new Vector2(MaxSpeed.X, Speed.Y);
            }
            else if (Keyboard.GetState().IsKeyDown(Input.Left) && AliveTimer > 0 && !IsWindingUp)
            {
                AnimationManager.Flip = false;
                WindUpAnimationManager.Flip = false;
                if (!IsInAir && !IsJumping) AnimationManager.Play(Animations["walk"]);

                if (IsGoingRight)
                {
                    Break();
                    return;
                }

                Accelerate();

                if (Speed.X < -MaxSpeed.X)
                    Speed = new Vector2(Speed.X - CurrentAcceleration, Speed.Y);
                else
                    Speed = new Vector2(-MaxSpeed.X, Speed.Y);
            }
            else Break();
        }

        private void Accelerate()
        {
            if (CurrentAcceleration < MaxAcceleration)
                CurrentAcceleration += Acceleration;
            else
                CurrentAcceleration = MaxAcceleration;
        }

        private void Decelerate()
        {
            if (CurrentAcceleration <= 0)
                CurrentAcceleration = 0;
            else
                CurrentAcceleration -= Acceleration;
        }

        private void Break()
        {
            if (IsStandingStill)
            {
                CurrentAcceleration = 0;
                return;
            }

            Decelerate();

            if (IsGoingRight)
            {
                Speed = new Vector2(Speed.X - CurrentAcceleration - Acceleration, Speed.Y);
                if (IsGoingLeft) Speed = new Vector2(0, Speed.Y);
            }
            else if (IsGoingLeft)
            {
                Speed = new Vector2(Speed.X + CurrentAcceleration + Acceleration, Speed.Y);
                if (IsGoingRight) Speed = new Vector2(0, Speed.Y);
            }
        }

        private void CheckJump()
        {
            if (!IsOnConveyor || IsWindingUp) return;
            if (CurrentKeyboard.IsKeyDown(Input.Jump) && PreviousKeyboard.IsKeyUp(Input.Jump) && AliveTimer > 0)
            {
                AnimationManager.Play(Animations["standing"]);
                AudioManager.PlayEffect(ContentManager.JumpSoundEffect);
                JumpLandingAnimation();
                IsJumping = true;
                Speed = Vector2.UnitY * JUMP_VELOCITY;
                FallAcceleration = 0;
                IsOnConveyor = false;
            }
        }

        private void JumpLandingAnimation()
        {
            ParticleManager.Textures = ContentManager.DustParticles;
            ParticleManager.EmitterLocation = new Vector2(Position.X + (Rectangle.Width / 2), Position.Y + Rectangle.Height - 10);
            ParticleManager.GenerateNewParticle(Microsoft.Xna.Framework.Color.White, 15, 10);
        }

    }

}
