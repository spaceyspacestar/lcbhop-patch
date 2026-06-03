using HarmonyLib;
using GameNetcodeStuff;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

namespace lcbhop {
    [HarmonyPatch( typeof( CharacterController ), "Move" )]
    class Move_Patch {
        [HarmonyPrefix]
        internal static bool Prefix( ref Vector3 motion ) {
            if (Plugin.player == null || !Plugin.player.enabled)
                return true;
            // Patch game movement when not called by us
            return !Plugin.patchMove;
        }
    }

    [HarmonyPatch( typeof( PlayerControllerB ), "Crouch_performed" )]
    class Crouch_performed_Patch {
        [HarmonyPrefix]
        internal static bool Prefix( PlayerControllerB __instance, ref InputAction.CallbackContext context ) {
            // Patch not being able to crouch when jumping

            if ( !context.performed ) {
                return false;
            }
            if ( __instance.quickMenuManager.isMenuOpen ) {
                return false;
            }
            if ( ( !__instance.IsOwner || !__instance.isPlayerControlled || ( __instance.IsServer && !__instance.isHostPlayerObject ) ) && !__instance.isTestingPlayer ) {
                return false;
            }
            if ( __instance.inSpecialInteractAnimation || __instance.isTypingChat ) {
                return false;
            }

            __instance.Crouch( !__instance.isCrouching );

            return false;
        }
    }

    [HarmonyPatch( typeof( PlayerControllerB ), "Jump_performed" )]
    class Jump_performed_Patch {
        [HarmonyPrefix]
        internal static bool Prefix( PlayerControllerB __instance, ref InputAction.CallbackContext context ) {
            if (Plugin.player == null || !Plugin.player.enabled)
                return true;
            
            // Beginning of original method to stop jumping when pressing spacebar in chat
            if ( __instance.quickMenuManager.isMenuOpen ) {
                return false;
            }
            if ( ( !__instance.IsOwner || !__instance.isPlayerControlled || ( __instance.IsServer && !__instance.isHostPlayerObject ) ) && !__instance.isTestingPlayer ) {
                return false;
            }
            if ( __instance.inSpecialInteractAnimation ) {
                return false;
            }
            if ( __instance.isTypingChat ) {
                return false;
            }
            
            Plugin.player.wishJump = true;

            // Patch jumping animation, we call it on our own
            return !Plugin.patchJump;
        }
    }

    [HarmonyPatch( typeof( PlayerControllerB ), "ScrollMouse_performed" )]
    class ScrollMouse_performed_Patch {
        [HarmonyPrefix]
        internal static bool Prefix( PlayerControllerB __instance, ref InputAction.CallbackContext context ) {
            // Patch scrolling in the hotbar if not autobhopping
            // Default scrolling behaviour when autohopping, in terminal, or mwheelup scroll
            return Plugin.cfg.autobhop || context.ReadValue<float>( ) > 0f || __instance.inTerminalMenu;
        }
    }


    [HarmonyPatch( typeof( PlayerControllerB ), "PlayerHitGroundEffects" )]
    class PlayerHitGroundEffects_Patch {
        // Fall damage and landing audio fix, overwrites method and applies a multiplier to fallvalues
        // Fallvalues get large very fast, moreso than vanilla, physics related? (single regular jump can see ~ -35f which is close to damage threshold of -38, vanilla is much lower ~ -14)
        [HarmonyPrefix]
        internal static bool Prefix( PlayerControllerB __instance ) {
            double fallMultiplier = 1.7;
            __instance.GetCurrentMaterialStandingOn( );
            if ( __instance.fallValueUncapped < -9f ) {
                if ( __instance.fallValueUncapped < -16f * 2) { // audio off slightly
                    __instance.movementAudio.PlayOneShot( StartOfRound.Instance.playerHitGroundHard, 1f );
                    WalkieTalkie.TransmitOneShotAudio( __instance.movementAudio, StartOfRound.Instance.playerHitGroundHard, 1f );
                } else if ( __instance.fallValueUncapped < -2f ) { 
                    __instance.movementAudio.PlayOneShot( StartOfRound.Instance.playerHitGroundSoft, 1f );
                }
               // __instance.LandFromJumpServerRpc( __instance.fallValueUncapped < -16f * 2 ); // try adjusting these for landing audio to align closer to vanilla
            }
            float num = __instance.fallValueUncapped;
            if ( __instance.disabledJetpackControlsThisFrame && Vector3.Angle( __instance.transform.up, Vector3.up ) > 80f ) {
                num -= 10f;
            }
            if ( __instance.takingFallDamage && !__instance.isSpeedCheating ) {
                if ( __instance.fallValueUncapped < -48.5f * fallMultiplier ) {
                    __instance.DamagePlayer( 100, true, true, CauseOfDeath.Gravity, 0, false, default( Vector3 ) );
                } else if ( __instance.fallValueUncapped < -45f * fallMultiplier ) {
                    __instance.DamagePlayer( 80, true, true, CauseOfDeath.Gravity, 0, false, default( Vector3 ) );
                } else if ( __instance.fallValueUncapped < -40f * fallMultiplier ) {
                    __instance.DamagePlayer( 50, true, true, CauseOfDeath.Gravity, 0, false, default( Vector3 ) );
                } else if ( __instance.fallValue < -38f * fallMultiplier ) {
                    __instance.DamagePlayer( 30, true, true, CauseOfDeath.Gravity, 0, false, default( Vector3 ) );
                }
            }
            if ( __instance.fallValueUncapped < -16f * 2) { //
                RoundManager.Instance.PlayAudibleNoise( __instance.transform.position, 7f, 0.5f, 0, false, 0 );
            }

            return false;
        }
    }

    [HarmonyPatch( typeof( HUDManager ), "SubmitChat_performed" )]
    class SubmitChat_performed_Patch {
        [HarmonyPrefix]
        internal static bool Prefix( HUDManager __instance ) {
            string text = __instance.chatTextField.text;

            if ( text.StartsWith( "/autobhop" ) || text.StartsWith( "/autohop" ) || text.StartsWith( "/ahop" ) ) {
                Plugin.cfg.autobhop = !Plugin.cfg.autobhop;
            } else if ( text.StartsWith( "/speedo" ) ) {
                Plugin.cfg.speedometer = !Plugin.cfg.speedometer;
            } else {
                return true;
            }

            __instance.localPlayer.isTypingChat = false;
            __instance.chatTextField.text = "";
            EventSystem.current.SetSelectedGameObject( null );
            __instance.PingHUDElement( __instance.Chat, 2f, 1f, 0.2f );
            __instance.typingIndicator.enabled = false;

            return false;
        }
    }

    [HarmonyPatch(typeof(PlayerControllerB), "ConnectClientToPlayerObject")]
    class PlayerSpawn_Patch {
        [HarmonyPostfix]
        static void Postfix(PlayerControllerB __instance) {
            if (!__instance.IsOwner)
                return;
            
            if (__instance.gameObject.GetComponent<CPMPlayer>() != null)
                return;
            
            Plugin.player = __instance.gameObject.AddComponent<CPMPlayer>();
            Plugin.player.player = __instance;
            Plugin.logger.LogInfo("CPMPlayer attached to " + __instance.playerUsername);
        }
    }
}
