using System;
using System.Collections.Generic;
using Unity.VisualScripting;
using UnityEngine;

namespace Celestial.CustomEndless
{
    public abstract class UnseenHandItem
    {
        protected readonly Unseen owner;
        protected readonly Item item;
        protected readonly HandItem source;
        protected float nextUseTime;
        private readonly List<Renderer> worldRenderers = new List<Renderer>();
        private GameObject visual;
        private Vector3 visualVelocity;
        private readonly List<UT_AudioClipHandler.AudioSet> audioSets = new List<UT_AudioClipHandler.AudioSet>();

        protected UnseenHandItem(Unseen owner, Item item, HandItem source)
        {
            this.owner = owner;
            this.item = item;
            this.source = source;
            CacheAudioSets();
            CreateVisual();
        }

        public void UpdateVisual()
        {
            if (visual == null) return;
            Vector3 direction = owner.GetUnseenHandItemDirection();
            Vector3 position = owner.GetUnseenHandItemOrigin();
            visual.transform.position = Vector3.SmoothDamp(visual.transform.position, position, ref visualVelocity, Mathf.Max(0.001f, owner.heldHandItemVisualPositionSmoothTime), Mathf.Infinity, Time.deltaTime);
            if (direction.sqrMagnitude > 0.0001f)
            {
                Quaternion rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
                float rotationBlend = 1f - Mathf.Exp(-Mathf.Max(0.01f, owner.heldHandItemVisualRotationSpeed) * Time.deltaTime);
                visual.transform.rotation = Quaternion.Slerp(visual.transform.rotation, rotation, rotationBlend);
            }
        }

        public void Release()
        {
            foreach (Renderer renderer in worldRenderers) if (renderer != null) renderer.enabled = true;
            worldRenderers.Clear();
            if (visual != null) UnityEngine.Object.Destroy(visual);
            visual = null;
        }

        protected bool IsAvailable()
        {
            return item != null && !item.used;
        }

        protected void Consume(bool destroy)
        {
            if (item == null) return;
            item.SetUsed(true);
            if (destroy) owner.DestroyUnseenHeldHandItem(item);
        }

        protected void PlayUseSound(params string[] names)
        {
            UT_AudioClipHandler.AudioSet selected = null;
            foreach (string name in names)
            {
                foreach (UT_AudioClipHandler.AudioSet set in audioSets)
                {
                    if (set == null || set.clips == null || set.clips.Count == 0 || string.IsNullOrEmpty(set.name)) continue;
                    if (!set.name.ContainsInsensitive(name)) continue;
                    selected = set;
                    break;
                }
                if (selected != null) break;
            }
            if (selected == null && audioSets.Count > 0) selected = audioSets[0];
            if (selected == null || selected.clips == null || selected.clips.Count == 0) return;
            AudioClip clip = selected.clips[UnityEngine.Random.Range(0, selected.clips.Count)];
            if (clip != null) AudioManager.PlaySound(clip, owner.GetUnseenHandItemOrigin(), UnityEngine.Random.Range(selected.minVolume, selected.maxVolume), UnityEngine.Random.Range(selected.minPitch, selected.maxPitch), Mathf.Clamp01(selected.spatialBlend));
        }

        private void CacheAudioSets()
        {
            if (source == null) return;
            UT_AudioClipHandler handler = source.GetComponent<UT_AudioClipHandler>();
            if (handler == null || handler.groups == null) return;
            foreach (UT_AudioClipHandler.AudioGroup group in handler.groups)
            {
                if (group == null || group.audioSets == null) continue;
                foreach (UT_AudioClipHandler.AudioSet set in group.audioSets) if (set != null && set.clips != null && set.clips.Count > 0) audioSets.Add(set);
            }
        }

        private void CreateVisual()
        {
            Item_Object carried = owner.GetUnseenHeldItemObject();
            if (carried != null) foreach (Renderer renderer in carried.GetComponentsInChildren<Renderer>(true))
                {
                    if (renderer == null) continue;
                    worldRenderers.Add(renderer);
                    renderer.enabled = false;
                }
            if (source == null || source.gameObject == null) return;
            visual = UnityEngine.Object.Instantiate(source.gameObject, owner.GetUnseenHandItemOrigin(), Quaternion.identity);
            foreach (HandItem handItem in visual.GetComponentsInChildren<HandItem>(true)) handItem.enabled = false;
            foreach (Hand_SpriteController controller in visual.GetComponentsInChildren<Hand_SpriteController>(true)) if (controller != null) controller.gameObject.SetActive(false);
            foreach (Collider collider in visual.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            UpdateVisual();
        }

        public static UnseenHandItem Create(Unseen owner, Item item)
        {
            if (owner == null || item == null || item.handItemAsset == null) return null;
            HandItem handItem = item.handItemAsset;
            if (handItem is HandItem_Shoot) return new UnseenHandItemShoot(owner, item, (HandItem_Shoot)handItem);
            if (handItem is HandItem_BlinkEye) return new UnseenHandItemBlinkEye(owner, item, (HandItem_BlinkEye)handItem);
            if (handItem is HandItem_Buff) return new UnseenHandItemConsumable(owner, item, (HandItem_Buff)handItem);
            if (handItem is HandItem_Food) return new UnseenHandItemFood(owner, item, (HandItem_Food)handItem);
            if (handItem is HandItem_Melee) return new UnseenHandItemMelee(owner, item, (HandItem_Melee)handItem);
            if (handItem is HandItem_GrapplingHook) return new UnseenHandItemGrapplingHook(owner, item, (HandItem_GrapplingHook)handItem);
            if (handItem is HandItem_Piton) return new UnseenHandItemPiton(owner, item, (HandItem_Piton)handItem);
            if (handItem is HandItem_Rewind) return new UnseenHandItemRewind(owner, item, (HandItem_Rewind)handItem);
            if (handItem is HandItem_Flashlight) return new UnseenHandItemFlashlight(owner, item, (HandItem_Flashlight)handItem);
            return new UnseenHandItemUtility(owner, item, handItem);
        }

        public abstract bool TryUse();

        protected bool Ready(float cooldown)
        {
            if (Time.time < nextUseTime) return false;
            nextUseTime = Time.time + Mathf.Max(0.05f, cooldown);
            return true;
        }

        protected static List<string> CopyTags(List<string> tags)
        {
            return tags != null ? new List<string>(tags) : new List<string>();
        }
    }

    public sealed class UnseenHandItemShoot : UnseenHandItem
    {
        private readonly GameObject projectile;
        private readonly float shootSpeed;
        private readonly float recoil;
        private readonly LayerMask aimMask;
        private readonly float initialInaccuracy;
        private readonly float addedInaccuracy;
        private readonly float maxInaccuracy;
        private readonly float inaccuracyLossRate;
        private readonly bool useAmmo;
        private readonly int maxAmmo;
        private readonly bool destroyAfterShoot;
        private float currentInaccuracy;

        public UnseenHandItemShoot(Unseen owner, Item item, HandItem_Shoot source) : base(owner, item, source)
        {
            projectile = source.projectile;
            projectile.GetComponent<Projectile>().damage = Math.Min(projectile.GetComponent<Projectile>().damage, 4);
            shootSpeed = source.shootSpeed;
            recoil = source.recoil;
            aimMask = source.aimMask;
            initialInaccuracy = source.initialShotInaccuracy;
            addedInaccuracy = source.addedShotInaccuracy;
            maxInaccuracy = source.maxShotInaccuracyMagnitude;
            inaccuracyLossRate = source.shotInaccuracyLoseRate;
            useAmmo = source.useAmmo;
            maxAmmo = source.maxAmmo;
            destroyAfterShoot = source.destroyItemAfterShoot || source is HandItem_Shoot_Spear || ContainsRebarName(item);
        }

        public override bool TryUse()
        {
            currentInaccuracy = Mathf.Max(0f, currentInaccuracy - Time.fixedDeltaTime * inaccuracyLossRate);
            GameEntity target = owner.GetUnseenHandItemTarget(Mathf.Max(12f, shootSpeed));
            if (!IsAvailable() || target == null || projectile == null || useAmmo && GetAmmo() <= 0 || !Ready(0.45f)) return false;
            Vector3 origin = owner.GetUnseenHandItemOrigin();
            Vector3 direction = target.transform.position + Vector3.up * 0.5f - origin;
            if (direction.sqrMagnitude < 0.001f) return false;
            Vector3 spread = UnityEngine.Random.insideUnitSphere * Mathf.Max(initialInaccuracy, currentInaccuracy);
            spread = Vector3.ClampMagnitude(spread, maxInaccuracy);
            direction = (direction.normalized + spread).normalized;
            GameObject instance = UnityEngine.Object.Instantiate(projectile, origin, Quaternion.LookRotation(direction));
            Projectile shot = instance.GetComponent<Projectile>();
            ENT_Player.GetPlayer().skewerable = false;
            if (shot == null)
            {
                UnityEngine.Object.Destroy(instance);
                return false;
            }
            shot.damageTags = CopyTags(shot.damageTags);
            if (!shot.damageTags.Contains("unseen")) shot.damageTags.Add("unseen");
            int playerLayer = LayerMask.NameToLayer("Player");
            int mask = aimMask.value == 0 ? Physics.DefaultRaycastLayers : aimMask.value;
            if (playerLayer >= 0) mask |= 1 << playerLayer;
            shot.hitMask = mask;
            shot.Initialize(direction * shootSpeed, null, item);
            PlayUseSound("shoot", "use");
            owner.ApplyUnseenHandItemRecoil(-direction * recoil);
            currentInaccuracy = Mathf.Min(maxInaccuracy, currentInaccuracy + addedInaccuracy);
            if (useAmmo) SetAmmo(GetAmmo() - 1);
            if (destroyAfterShoot) Consume(true);
            return true;
        }

        private int GetAmmo()
        {
            string value = item.GetFirstDataStringByType("ammo");
            int ammo;
            return int.TryParse(value, out ammo) ? Mathf.Max(0, ammo) : Mathf.Max(0, maxAmmo);
        }

        private static bool ContainsRebarName(Item item)
        {
            if (item == null) return false;
            return ContainsRebarText(item.itemName) || ContainsRebarText(item.prefabName) || ContainsRebarText(item.itemTag);
        }

        private static bool ContainsRebarText(string value)
        {
            return !string.IsNullOrEmpty(value) && value.ContainsInsensitive("rebar");
        }

        private void SetAmmo(int ammo)
        {
            item.SetFirstDataStringsofType("ammo", Mathf.Max(0, ammo).ToString());
        }
    }

    public sealed class UnseenHandItemBlinkEye : UnseenHandItem
    {
        private readonly LayerMask blinkMask;
        private readonly float blinkDistance;
        private readonly float cooldown;
        private readonly bool crushOnLastUse;

        public UnseenHandItemBlinkEye(Unseen owner, Item item, HandItem_BlinkEye source) : base(owner, item, source)
        {
            blinkMask = source.blinkMask;
            blinkDistance = source.blinkDistance;
            cooldown = Mathf.Max(1f, source.crushOnLastUse ? 3f : 1.5f);
            crushOnLastUse = source.crushOnLastUse;
        }

        public override bool TryUse()
        {
            GameEntity target = owner.GetUnseenHandItemTarget(blinkDistance * 2f);
            Vector3 direction = target != null ? target.transform.position - owner.GetUnseenHandItemOrigin() : owner.GetUnseenHandItemDirection();
            int uses = GetUses();
            if (!IsAvailable() || uses <= 0 || direction.sqrMagnitude < 0.001f || !Ready(cooldown)) return false;
            if (!owner.TryUnseenHandItemBlink(direction.normalized, blinkDistance, blinkMask)) return false;
            uses--;
            item.SetFirstDataStringsofType("uses", uses.ToString());
            if (uses <= 0 && crushOnLastUse) Consume(true);
            PlayUseSound("warp", "squish", "eye");
            return true;
        }

        private int GetUses()
        {
            string value = item.GetFirstDataStringByType("uses");
            int uses;
            return int.TryParse(value, out uses) ? uses : 1;
        }
    }

    public sealed class UnseenHandItemConsumable : UnseenHandItem
    {
        private readonly BuffContainer buff;
        private readonly List<BuffContainer> secondaryBuffs;
        private readonly bool useSecondaryBuffs;
        private readonly float healAmount;

        public UnseenHandItemConsumable(Unseen owner, Item item, HandItem_Buff source) : base(owner, item, source)
        {
            buff = source.buff != null ? source.buff.CopyBuffContainer() : null;
            secondaryBuffs = new List<BuffContainer>();
            if (source.secondaryBuffs != null) foreach (BuffContainer value in source.secondaryBuffs) if (value != null) secondaryBuffs.Add(value.CopyBuffContainer());
            useSecondaryBuffs = source.useSecondaryBuffs;
            healAmount = source.healAmount;
        }

        public override bool TryUse()
        {
            if (!IsAvailable() || !Ready(8f)) return false;
            owner.ApplyUnseenHandItemConsumable(buff, useSecondaryBuffs ? secondaryBuffs : null, healAmount);
            PlayUseSound("use", "consume");
            Consume(true);
            return true;
        }
    }

    public sealed class UnseenHandItemFood : UnseenHandItem
    {
        private readonly float gripAmount;
        private readonly float healAmount;
        private readonly BuffContainer buff;
        private readonly bool useBuff;

        public UnseenHandItemFood(Unseen owner, Item item, HandItem_Food source) : base(owner, item, source)
        {
            gripAmount = source.gripAmountAdded;
            healAmount = source.healAmount;
            buff = source.buff != null ? source.buff.CopyBuffContainer() : null;
            useBuff = source.useBuff;
        }

        public override bool TryUse()
        {
            if (!IsAvailable() || !Ready(7f)) return false;
            owner.ApplyUnseenHandItemConsumable(useBuff ? buff : null, null, healAmount + gripAmount * 0.1f);
            PlayUseSound("eat", "use");
            Consume(true);
            return true;
        }
    }

    public sealed class UnseenHandItemMelee : UnseenHandItem
    {
        private readonly float damage;
        private readonly float range;
        private readonly List<string> damageTags;
        private readonly float force;

        public UnseenHandItemMelee(Unseen owner, Item item, HandItem_Melee source) : base(owner, item, source)
        {
            damage = source.damage;
            range = source.range;
            damageTags = CopyTags(source.damageTags);
            force = Mathf.Max(8f, source.hammerAmount * 40f);
        }

        public override bool TryUse()
        {
            GameEntity target = owner.GetUnseenHandItemTarget(range + 1f);
            if (target == null || Vector3.Distance(owner.GetUnseenHandItemOrigin(), target.transform.position) > range + 1f || !Ready(0.8f)) return false;
            List<string> tags = CopyTags(damageTags);
            if (!tags.Contains("unseen")) tags.Add("unseen");
            target.Damage(Damageable.DamageInfo.CreateDamageInfo(damage, "Melee", tags));
            Vector3 direction = target.transform.position - owner.GetUnseenHandItemOrigin();
            if (direction.sqrMagnitude > 0.001f) target.AddForce(direction.normalized * force, "unseen");
            owner.ApplyUnseenHandItemRecoil(-direction.normalized * force * 0.08f);
            PlayUseSound("swing", "use");
            return true;
        }
    }

    public sealed class UnseenHandItemGrapplingHook : UnseenHandItem
    {
        private readonly float distance;
        private readonly float force;
        private readonly LayerMask mask;

        public UnseenHandItemGrapplingHook(Unseen owner, Item item, HandItem_GrapplingHook source) : base(owner, item, source)
        {
            distance = source.grappleDistance;
            force = source.grappleForce;
            mask = source.grappleMask;
        }

        public override bool TryUse()
        {
            GameEntity target = owner.GetUnseenHandItemTarget(distance);
            Vector3 direction = target != null ? target.transform.position - owner.GetUnseenHandItemOrigin() : owner.GetUnseenHandItemDirection();
            if (direction.sqrMagnitude < 0.001f || !Ready(1.4f)) return false;
            bool used = owner.TryUnseenHandItemGrapple(direction.normalized, distance, force, mask);
            if (used) PlayUseSound("grapple", "use");
            return used;
        }
    }

    public sealed class UnseenHandItemPiton : UnseenHandItem
    {
        private readonly float damage;
        private readonly float distance;
        private readonly LayerMask mask;

        public UnseenHandItemPiton(Unseen owner, Item item, HandItem_Piton source) : base(owner, item, source)
        {
            damage = source.damage;
            distance = source.placeDistance;
            mask = source.damageMask;
        }

        public override bool TryUse()
        {
            GameEntity target = owner.GetUnseenHandItemTarget(distance + 1f);
            if (target == null || Vector3.Distance(owner.GetUnseenHandItemOrigin(), target.transform.position) > distance + 1f || !Ready(1.2f)) return false;
            target.Damage(Damageable.DamageInfo.CreateDamageInfo(damage, "piton", new List<string> { "unseen" }));
            owner.TryUnseenHandItemGrapple((target.transform.position - owner.GetUnseenHandItemOrigin()).normalized, distance, 5f, mask);
            PlayUseSound("use", "piton");
            return true;
        }
    }

    public sealed class UnseenHandItemRewind : UnseenHandItem
    {
        private readonly float staminaRegen;
        private readonly float healthRegen;

        public UnseenHandItemRewind(Unseen owner, Item item, HandItem_Rewind source) : base(owner, item, source)
        {
            staminaRegen = source.staminaRegenRate;
            healthRegen = source.healthRegenRate;
        }

        public override bool TryUse()
        {
            if (!Ready(5f)) return false;
            PlayUseSound("use", "rewind");
            return true;
        }
    }

    public sealed class UnseenHandItemFlashlight : UnseenHandItem
    {
        private readonly float chargeRate;

        public UnseenHandItemFlashlight(Unseen owner, Item item, HandItem_Flashlight source) : base(owner, item, source)
        {
            chargeRate = Mathf.Max(0.1f, source.lightPosition != null ? 1f : 0.5f);
        }

        public override bool TryUse()
        {
            if (!Ready(4f)) return false;
            PlayUseSound("pump", "use");
            return true;
        }
    }

    public sealed class UnseenHandItemUtility : UnseenHandItem
    {
        public UnseenHandItemUtility(Unseen owner, Item item, HandItem source) : base(owner, item, source)
        {
        }

        public override bool TryUse()
        {
            if (!Ready(6f)) return false;
            PlayUseSound("use");
            return true;
        }
    }
}