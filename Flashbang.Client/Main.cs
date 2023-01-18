global using CitizenFX.Core;
global using CitizenFX.Core.Native;
using FxEvents;
using Logger;
using System;
using System.Threading.Tasks;
using Flashbang.Shared;

namespace Flashbang.Client
{
    public class Main : BaseScript
    {
        private const string PTFX_DICT = "core";
        private const string PTFX_ASSET = "ent_anim_paparazzi_flash";
        
        private const string WEAPON_FLASHBANG = "WEAPON_FLASHBANG";
        private const string WEAPON_FLASHBANG_MODEL = "w_ex_flashbang";

        private const string ANIMATION_DICT = "anim@heists@ornate_bank@thermal_charge";
        private const string ANIMATION_ENTER = "cover_eyes_intro";
        private const string ANIMATION_EXIT = "cover_eyes_exit";

        private const float MAX_CAMERA_SHAKE_AMPLITUDE = 25.0f;
        private const float MAX_CAMERA_SHAKE_AFTER_AMPLITUDE = 18.0f;
        
        private bool _flashbangEquipped = false;
        private float _totalFlashShakeAmp = 0.0f;
        private float _totalAfterShakeAmp = 0.0f;
        private int _flashTimersRunning = 0;
        private bool _isCameraShakeEnabled = false;
        private int _afterTimersRunning = 0;

        Log Log = new();

        public Main()
        {
            API.AddTextEntry("WT_GNADE_FLSH", "Flashbang");

            EventDispatcher.Mount("Flashbang:Explode", new Action<FlashbangMessage>(OnFlashbangExplodeAsync));
            
            Tick += OnFlashbangAsync;
        }

        private async void SendFlashbangThrownMessage(int propHandle)
        {
            Prop flashbang = (Prop)Entity.FromHandle(propHandle);
            await Delay(1500);
            
            if (!flashbang.Exists()) return;
            Vector3 flashbangPos = flashbang.Position;
            World.AddExplosion(flashbangPos, ExplosionType.ProgramAR, 0f, 1f, null, true, true);
            
            FlashbangMessage flashbangMessage = new();
            flashbangMessage.Position = flashbangPos;
            flashbangMessage.Prop = propHandle;

            EventDispatcher.Send("Flashbang:DispatchExplosion", flashbangMessage);

            if (flashbang.Exists())
                flashbang.Delete();
        }

        private void SetCameraShakeAmplitude(float amp)
        {
            if (_isCameraShakeEnabled)
            {
                GameplayCamera.ShakeAmplitude = amp;
            }
            else
            {
                GameplayCamera.Shake(CameraShake.Hand, amp);
                _isCameraShakeEnabled = true;
            }
        }

        private async void SetCameraShakeAmplitudeFalloff(float amp)
        {
            float currentAmp = amp;
            float ampInterval = amp / 2;
            while (currentAmp > 0.0f)
            {
                currentAmp = currentAmp - ampInterval;
                GameplayCamera.ShakeAmplitude = currentAmp;
                await Delay(500);
            }
            GameplayCamera.StopShaking();
            _isCameraShakeEnabled = false;
        }

        private async void DisablePlayerFromUsingWeapons(int time)
        {
            int finTime = Game.GameTime + time;
            while (Game.GameTime < finTime)
            {
                await Delay(0);
                Game.Player.DisableFiringThisFrame();
            }
        }

        private float ValidateFlashbangCameraShakeAmplitude(float amplitude)
        {
            if (amplitude < 0.0f)
            {
                return (0.0f);
            }
            else
            {
                if (amplitude > MAX_CAMERA_SHAKE_AMPLITUDE)
                {
                    return (MAX_CAMERA_SHAKE_AMPLITUDE);
                }
                else
                {
                    return (amplitude);
                }
            }
        }

        private float ValidateAfterFlashbangCameraShakeAmplitude(float amplitude)
        {
            if (amplitude < 0.0f)
            {
                return (0.0f);
            }
            else
            {
                if (amplitude > MAX_CAMERA_SHAKE_AFTER_AMPLITUDE)
                {
                    return (MAX_CAMERA_SHAKE_AFTER_AMPLITUDE);
                }
                else
                {
                    return (amplitude);
                }
            }
        }

        private async void AfterFlashbangEffect(float shakeAmplitude, int duration)
        {
            // Buffs
            _afterTimersRunning++;
            _totalAfterShakeAmp += shakeAmplitude;
            SetCameraShakeAmplitude(ValidateAfterFlashbangCameraShakeAmplitude(_totalAfterShakeAmp));

            // Wait
            await Delay(duration);

            // Debuffs
            _afterTimersRunning--;
            _totalAfterShakeAmp -= shakeAmplitude;

            // Cleanup
            if (_flashTimersRunning == 0)
            {
                SetCameraShakeAmplitude(ValidateAfterFlashbangCameraShakeAmplitude(_totalAfterShakeAmp));
                if ((_afterTimersRunning == 0) && (_flashTimersRunning == 0))
                {
                    SetCameraShakeAmplitudeFalloff(_totalFlashShakeAmp + shakeAmplitude);
                    API.AnimpostfxStop("Dont_tazeme_bro");
                }
            }
        }

        private async void FlashbangEffect(float shakeAmplitude, int duration, float afterShakeAmplitude, int afterEffectDuration)
        {
            Ped ped = Game.Player.Character;

            // Buffs
            _flashTimersRunning++;
            _totalFlashShakeAmp += shakeAmplitude;
            DisablePlayerFromUsingWeapons(duration);
            SetCameraShakeAmplitude(ValidateFlashbangCameraShakeAmplitude(_totalFlashShakeAmp));

            API.RequestAnimDict(ANIMATION_DICT);

            // Animation and screen effect
            if (_flashTimersRunning == 1)
            {
                API.AnimpostfxPlay("Dont_tazeme_bro", 0, true);
                await ped.Task.PlayAnimation(ANIMATION_DICT, ANIMATION_ENTER, -8f, -8f, -1, AnimationFlags.StayInEndFrame | AnimationFlags.UpperBodyOnly | AnimationFlags.AllowRotation, 8f);
            }

            // Wait
            await Delay(duration);

            // Debuffs
            _flashTimersRunning--;
            _totalFlashShakeAmp -= shakeAmplitude;
            SetCameraShakeAmplitude(ValidateFlashbangCameraShakeAmplitude(_totalFlashShakeAmp));

            // Cleanup
            if (_flashTimersRunning == 0)
            {
                await ped.Task.PlayAnimation(ANIMATION_DICT, ANIMATION_EXIT, -8f, -8f, -1, AnimationFlags.StayInEndFrame | AnimationFlags.UpperBodyOnly | AnimationFlags.AllowRotation, 8f);
                AfterFlashbangEffect(afterShakeAmplitude, afterEffectDuration);
            }

            API.RemoveAnimDict(ANIMATION_DICT);
        }

        private void ApplyDamageToPedIfInLethalRadius(Ped ped, Vector3 position, float lethalRaduis, int damage)
        {
            if (ped.IsInRangeOf(position, lethalRaduis))
            {
                ped.ApplyDamage(damage);
                Log.Debug($"Applying Damage Amount: {damage}");
            }
        }

        private async void OnFlashbangExplodeAsync(FlashbangMessage message)
        {
            Ped ped = Game.PlayerPed;
            int pedHandle = ped.Handle;
            
            bool hit = false;
            int entityHit = 0;
            int result = 1;
            bool playerHit = false;
            Vector3 hitPos = new Vector3();
            Vector3 surfaceNormal = new Vector3();
            Vector3 pedPos = API.GetPedBoneCoords(pedHandle, 0x62ac, 0, 0, 0);
            Vector3 pedPos2 = API.GetPedBoneCoords(pedHandle, 0x6b52, 0, 0, 0);
            Vector3 pos = message.Position;
            Vector3 hitReg = new Vector3(pedPos.X - message.Position.X, pedPos.Y - message.Position.X, pedPos.Z - message.Position.Z); // Richtungsvektor
            PlayParticleEffectAtPosition(pos);

            float distance = World.GetDistance(ped.Position, pos);
            float faceDistance = World.GetDistance(pedPos, pos);
            float distanceSq = distance * distance;

            float effectFalloffMultiplier = 0.02f / (message.Range / 8.0f); // Radius ~< effectFalloffMultiplier
            float stunTimeMultiplier = effectFalloffMultiplier * distanceSq; // Distanz ~> effectFalloffMultiplier
            int effectFalloffStunTime;
            int effectFalloffAfterTime;
            float shakeCamAmp = 15.0f;
            effectFalloffStunTime = (int)(message.StunDuration * stunTimeMultiplier); // Distanz ~> FalloffStunTime
            effectFalloffAfterTime = (int)(message.AfterStunDuration * stunTimeMultiplier);

            int actualStunTime = (message.StunDuration - effectFalloffStunTime) * 1000;
            int actualAfterTime = (message.AfterStunDuration - effectFalloffAfterTime) * 1000;
            float stunTimeFallOffMultiplier = ((float)actualStunTime) / ((float)(message.StunDuration * 1000));

            if (actualStunTime <= 0)
            {
                actualStunTime = 1;
            }

            if (actualAfterTime <= 0)
            {
                actualAfterTime = 1;
            }

            int handle = API.StartShapeTestLosProbe(pos.X, pos.Y, pos.Z, pedPos.X + (10 * hitReg.X), pedPos.Y + (10 * hitReg.Y), pedPos.Z + (10 * hitReg.Z), 0b0000_0000_1001_1111, message.Prop, 0b0000_0000_0000_0100);

            while (result == 1)
            {
                result = API.GetShapeTestResult(handle, ref hit, ref hitPos, ref surfaceNormal, ref entityHit);
                await Delay(0);
            }
            playerHit = (result == 2) && (entityHit == pedHandle);

            handle = API.StartShapeTestLosProbe(pos.X, pos.Y, pos.Z, pedPos2.X + (10 * hitReg.X), pedPos2.Y + (10 * hitReg.Y), pedPos2.Z + (10 * hitReg.Z), 0b0000_0000_1001_1111, message.Prop, 0b0000_0000_0000_0100);

            while (handle == 1)
            {
                API.GetShapeTestResult(handle, ref hit, ref hitPos, ref surfaceNormal, ref entityHit);
                await Delay(0);
            }
            playerHit = playerHit || ((result == 2) && (entityHit == pedHandle));

            if ((faceDistance <= message.Range) && playerHit)
            {
                ApplyDamageToPedIfInLethalRadius(ped, pos, message.LethalRadius, message.Damage);
                FlashbangEffect(shakeCamAmp * stunTimeFallOffMultiplier, actualStunTime, 10f * stunTimeFallOffMultiplier, actualAfterTime);
            }
        }

        private async Task OnFlashbangAsync()
        {
            Ped playerPed = Game.PlayerPed;
            if (!_flashbangEquipped)
            {
                if (playerPed.Weapons.Current.Hash == (WeaponHash)API.GetHashKey(WEAPON_FLASHBANG))
                {
                    _flashbangEquipped = true;
                }
            }
            else
            {
                if (playerPed.IsShooting)
                {
                    _flashbangEquipped = false;

                    await Delay(100);

                    Vector3 pos = playerPed.Position;
                    int objectHandle = API.GetClosestObjectOfType(pos.X, pos.Y, pos.Z, 50f, (uint)API.GetHashKey(WEAPON_FLASHBANG_MODEL), false, false, false);

                    if (objectHandle != 0)
                    {
                        SendFlashbangThrownMessage(objectHandle);
                    }
                }
            }
            await Task.FromResult(0);
        }

        private async void PlayParticleEffectAtPosition(Vector3 pos)
        {
            API.RequestNamedPtfxAsset(PTFX_DICT);
            while (!API.HasNamedPtfxAssetLoaded(PTFX_DICT))
            {
                await Delay(0);
            }
            API.UseParticleFxAssetNextCall(PTFX_DICT);
            API.StartParticleFxLoopedAtCoord(PTFX_ASSET, pos.X, pos.Y, pos.Z, 0f, 0f, 0f, 25f, false, false, false, false);
        }
    }
}
