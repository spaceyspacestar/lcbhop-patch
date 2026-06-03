/*
 * Based on https://github.com/WiggleWizard/quake3-movement-unity3d/blob/master/CPMPlayer.cs
 * Modified to match https://github.com/ValveSoftware/halflife/blob/master/pm_shared/pm_shared.c
 */

using System;
//using System.Numerics;
using System.Reflection;

using GameNetcodeStuff;

using TMPro;

using UnityEngine;
using UnityEngine.InputSystem;

namespace lcbhop {
    // Contains the command the user wishes upon the character
    struct Cmd {
        public float forwardMove;
        public float rightMove;
        public float upMove;
    }

    public class CPMPlayer : MonoBehaviour {
        /* Frame occuring factors */
        public float gravity = Plugin.cfg.gravity;              // Gravity

        public float friction = Plugin.cfg.friction;            // Ground friction

        /* Movement stuff */
        public float maxspeed = Plugin.cfg.maxspeed;            // Max speed
        public float movespeed = Plugin.cfg.movespeed;          // Ground speed (like cl_forwardspeed etc.)
        public float walkspeed = Plugin.cfg.walkspeed;
        public float accelerate = Plugin.cfg.accelerate;        // Ground acceleration
        public float airaccelerate = Plugin.cfg.airaccelerate;  // Air acceleration
        public float stopspeed = Plugin.cfg.stopspeed;          // Ground deceleration

        public PlayerControllerB player;
        private CharacterController _controller;

        private Vector3 velocity = Vector3.zero;

        public bool wishJump = false;

        // Player commands, stores wish commands that the player asks for (Forward, back, jump, etc)
        private Cmd _cmd;

        private GameObject compass;
        private TextMeshProUGUI speedo;

        private Vector3 _lastNormal;

        private void Start( ) {
            _controller = player.thisController;
        }

        private void Update( ) {
            // Allow crouching while mid air
            player.fallValue = 0.0f;
            // Disables fall damage
            if ( !Plugin.cfg.falldmg ) { player.fallValueUncapped = 0.0f; }
            // Disable stamina drain if toggle is false
            if ( !Plugin.cfg.staminadrain ) { player.sprintMeter = 1.0f; }

            //Console.WriteLine( "fallValue: " + player.fallValue + " \tfallValueUncapped: " + player.fallValueUncapped + " \ttaking damage: " + player.takingFallDamage + " \t" + player.health );

            if ( ( !player.IsOwner || !player.isPlayerControlled || ( player.IsServer && !player.isHostPlayerObject ) ) && !player.isTestingPlayer ) {
                return;
            }
            if ( player.quickMenuManager.isMenuOpen || player.inSpecialInteractAnimation || player.isTypingChat ) {
                return;
            }

            // Don't patch movement on ladders
            if ( player.isClimbingLadder ) {
                Plugin.patchMove = false;
                velocity = Vector3.zero;
                return;
            }

            //Console.WriteLine( "injured: " + player.criticallyInjured );

            /* Movement, here's the important part */
            Jump( );

            if ( !wishJump && _controller.isGrounded )
                Friction( );

            if ( _controller.isGrounded )
                WalkMove( );
            else if ( !_controller.isGrounded )
                AirMove( );

            // Move the controller
            Plugin.patchMove = false; // Disable the Move Patch
            
            try {
                _controller.Move( velocity / 32.0f * Time.deltaTime );
            } 
            finally {
                Plugin.patchMove = true; // Reenable the Move Patch
            }
            

            if (_lastNormal != Vector3.zero)
            {
                float backoff = Vector3.Dot(velocity, _lastNormal);
                if (backoff < 0)
                    velocity -= _lastNormal * backoff;
                
                _lastNormal = Vector3.zero;
            }

            wishJump = false;

            /* Speedometer */
            if ( Plugin.cfg.speedometer ) {
                if ( !compass ) {
                    compass = GameObject.Find( "/Systems/UI/Canvas/IngamePlayerHUD/TopLeftCorner/Compass" );
                    speedo = compass.GetComponentInChildren<TextMeshProUGUI>( );
                }
                if ( !compass )
                    return;

                compass.SetActive( true );

                // Only X, Y speed
                Vector3 vel = velocity;
                vel.y = 0.0f;

                speedo.text = $"{( int ) vel.magnitude} u";
                speedo.rectTransform.sizeDelta = speedo.GetPreferredValues( );
            } else {
                if ( compass )
                    compass.SetActive( false );
            }
        }

        /*******************************************************************************************************\
        |* MOVEMENT
        \*******************************************************************************************************/

        /*
         * Sets the movement direction based on player input
         */
        private void SetMovementDir( ) {
            float speed = 0;
            // Checks if player is sprinting or not and sets ground speed
            if ( player.isSprinting ) {
                speed = movespeed;
            } else {
                speed = walkspeed;
            }
            //Console.Out.WriteLine( "Speed: " + speed / player.carryWeight );
            player.CalculateGroundNormal( ); // Default logic
            // Adjust speed based on weight
            speed /= player.carryWeight; // This variable is pre calculated somewhere using formula: 1 + (TOTALWEIGHT / 105)

            // Default logic
            if ( player.sinkingValue > 0.73f ) {
                speed = 0f;
            } else {
                if ( player.isCrouching ) {
                    speed /= 1.5f;
                } else if ( player.criticallyInjured && !player.isCrouching ) {
                    speed *= player.limpMultiplier;
                }
                if ( player.isSpeedCheating ) {
                    speed *= 15f;
                }
                if ( player.movementHinderedPrev > 0 ) {
                    speed /= 2f * player.hinderedMultiplier;
                }
                if ( player.drunkness > 0f ) {
                    speed *= StartOfRound.Instance.drunknessSpeedEffect.Evaluate( player.drunkness ) / 5f + 1f;
                }
                if ( !player.isCrouching && player.crouchMeter > 1.2f ) {
                    speed *= 0.5f;
                }
                if ( !player.isCrouching ) {
                    float num4 = Vector3.Dot( player.playerGroundNormal, player.walkForce );
                    if ( num4 > 0.05f ) {
                        player.slopeModifier = Mathf.MoveTowards( player.slopeModifier, num4, ( player.slopeModifierSpeed + 0.45f ) * Time.deltaTime );
                    } else {
                        player.slopeModifier = Mathf.MoveTowards( player.slopeModifier, num4, player.slopeModifierSpeed / 2f * Time.deltaTime );
                    }
                    speed = Mathf.Max( speed * 0.8f, speed + player.slopeIntensity * player.slopeModifier );
                }
            }
            if ( player.isTypingChat || ( player.jetpackControls && !player.thisController.isGrounded ) || StartOfRound.Instance.suckingPlayersOutOfShip ) {
                player.moveInputVector = Vector2.zero;
            }

            //_cmd.forwardMove = player.playerActions.Movement.Move.ReadValue<Vector2>( ).y * speed;
            //_cmd.rightMove = player.playerActions.Movement.Move.ReadValue<Vector2>( ).x * speed;
            _cmd.forwardMove = player.moveInputVector.y * speed; // Reading from player Vector2 instead of unity Action
            _cmd.rightMove = player.moveInputVector.x * speed;
        }
        
        /*
         * Checks for jump input
         */
        private void Jump( ) {
            if ( Plugin.cfg.autobhop ) {
                wishJump = player.playerActions.Movement.Jump.ReadValue<float>( ) > 0.0f;
                //wishJump = player.isJumping; // private var, assembly needs to be Publicized
            } else if ( !wishJump ) {
                //wishJump = player.playerActions.Movement.SwitchItem.ReadValue<float>( ) != 0.0f;
                wishJump = player.playerActions.Movement.SwitchItem.ReadValue<float>( ) < 0f; // Jump only if you scroll down
            }

            if ( !Plugin.cfg.enablebunnyhopping ) {
                PreventMegaBunnyJumping( );
            }
        }

        /*
         * Execs when the player is in the air
         */
        private void AirMove( ) {
            Vector3 wishvel;
            Vector3 wishdir;
            float wishspeed;

            SetMovementDir( );

            wishvel = new Vector3( _cmd.rightMove, 0, _cmd.forwardMove );
            wishvel = transform.TransformDirection( wishvel );

            wishdir = wishvel;

            wishspeed = wishdir.magnitude;
            wishdir.Normalize( );

            if ( wishspeed > maxspeed ) {
                wishvel *= maxspeed / wishspeed;
                wishspeed = maxspeed;
            }

            AirAccelerate( wishdir, wishspeed, airaccelerate );

            // Apply gravity
            velocity.y -= gravity * Time.deltaTime;
        }

        /*
         * Called every frame when the engine detects that the player is on the ground
         */
        private void WalkMove( ) {
            Vector3 wishvel;
            Vector3 wishdir;
            float wishspeed;

            SetMovementDir( );

            wishvel = new Vector3( _cmd.rightMove, 0, _cmd.forwardMove );
            wishvel = transform.TransformDirection( wishvel );

            wishdir = wishvel;

            wishspeed = wishdir.magnitude;
            wishdir.Normalize( );

            if ( wishspeed > maxspeed ) {
                wishvel *= maxspeed / wishspeed;
                wishspeed = maxspeed;
            }

            Accelerate( wishdir, wishspeed, accelerate );

            // Reset the gravity velocity
            velocity.y = -gravity * Time.deltaTime;

            if ( wishJump ) {
                velocity.y = 295;
                
                // Jump audio for server and client
                player.PlayJumpAudio( );
                player.PlayerJumpedServerRpc( );

                // Animate player jumping, this is a bit tricky since its a private method (there's probably a better way to do this)
                /* XXX: This messes with the animator and makes you not be able to crouch, couldnt figure it out yet!
                 * Plugin.patchJump = false; // Disable jump patch
                 * MethodInfo method = player.GetType( ).GetMethod( "Jump_performed", BindingFlags.NonPublic | BindingFlags.Instance );
                 * method.Invoke( player, new object[] { new InputAction.CallbackContext( ) } ); // Pass dummy callback context
                 * Plugin.patchJump = true; // Reenable jump patch
                 */ 
                 
            }
        }

        /*
         * Applies friction to the player, called in both the air and on the ground
         */
        private void Friction( ) {
            Vector3 vec = velocity;
            float speed;
            float newspeed;
            float control;
            float drop;

            vec.y = 0.0f;
            speed = vec.magnitude;

            if ( speed < 0.1f )
                return;

            drop = 0.0f;

            /* Only if the player is on the ground then apply friction */
            if ( _controller.isGrounded ) {
                control = ( speed < stopspeed ) ? stopspeed : speed;
                drop += control * friction * Time.deltaTime;
            }

            newspeed = speed - drop;
            if ( newspeed < 0 )
                newspeed = 0;

            newspeed /= speed;

            velocity.x *= newspeed;
            velocity.z *= newspeed;
        }

        private void Accelerate( Vector3 wishdir, float wishspeed, float accel ) {
            float addspeed;
            float accelspeed;
            float currentspeed;

            currentspeed = Vector3.Dot( velocity, wishdir );

            addspeed = wishspeed - currentspeed;

            if ( addspeed <= 0 )
                return;

            accelspeed = accel * Time.deltaTime * wishspeed;

            if ( accelspeed > addspeed )
                accelspeed = addspeed;

            velocity.x += accelspeed * wishdir.x;
            velocity.z += accelspeed * wishdir.z;
        }

        private void AirAccelerate( Vector3 wishdir, float wishspeed, float accel ) {
            float addspeed;
            float accelspeed;
            float currentspeed;
            float wishspd = wishspeed;

            if ( wishspd > 30 )
                wishspd = 30;

            currentspeed = Vector3.Dot( velocity, wishdir );

            addspeed = wishspd - currentspeed;

            if ( addspeed <= 0 )
                return;

            accelspeed = accel * wishspeed * Time.deltaTime;

            if ( accelspeed > addspeed )
                accelspeed = addspeed;

            velocity.x += accelspeed * wishdir.x;
            velocity.z += accelspeed * wishdir.z;
        }

        private void PreventMegaBunnyJumping( ) {
            Vector3 vec = velocity;
            float spd;
            float fraction;
            float maxscaledspeed;

            maxscaledspeed = 1.7f /* BUNNYJUMP_MAX_SPEED_FACTOR */ * maxspeed;

            if ( maxscaledspeed <= 0.0f )
                return;

            vec.y = 0.0f;
            spd = vec.magnitude;

            if ( spd <= maxscaledspeed )
                return;

            fraction = ( maxscaledspeed / spd ) * 0.65f;

            velocity.x *= fraction;
            velocity.z *= fraction;
        }
    }
}