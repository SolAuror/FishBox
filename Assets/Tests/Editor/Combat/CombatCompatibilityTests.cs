using NUnit.Framework;
using Sol;
using Sol.Actions;
using Sol.AI;
using Sol.Combat;
using Sol.Grab;
using Sol.Player;
using UnityEditor;
using UnityEngine;

public sealed class CombatCompatibilityTests
{
    [Test]
    public void CombatResolver_UnarmoredTargetReceivesFullDamage()
    {
        GameObject attackerObject = new("Attacker");
        GameObject targetObject = new("Target");
        try
        {
            Combatant attacker = AddNpcCombatant(attackerObject, health: 100f, stamina: 100f);
            Combatant target = AddNpcCombatant(targetObject, health: 100f, stamina: 100f);

            CombatDamageResult result = CombatResolver.ApplyHit(
                new CombatHit(attacker, target, null, 25f, 7f, 0f, CombatAttackKind.Unarmed, Vector3.forward));

            Assert.That(result.Applied, Is.True);
            Assert.That(result.OriginalDamage, Is.EqualTo(25f).Within(0.001f));
            Assert.That(result.FinalDamage, Is.EqualTo(25f).Within(0.001f));
            Assert.That(result.ArmorRating, Is.EqualTo(0f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(75f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void CombatResolver_ArmorMitigationUsesFormulaAndCapsAtEightyPercent()
    {
        GameObject attackerObject = new("Attacker");
        GameObject targetObject = new("Armored Target");
        GameObject armorObject = new("Plate Armor");
        try
        {
            Combatant attacker = AddNpcCombatant(attackerObject, health: 100f, stamina: 100f);
            Combatant target = AddNpcCombatant(targetObject, health: 1000f, stamina: 100f);
            Equipment equipment = targetObject.AddComponent<Equipment>();
            AddBone(targetObject, "Chest");
            ItemComponent armor = AddItem(armorObject, ItemType.Armor, equipBone: "Chest", defense: 1000f);

            Assert.That(equipment.Equip(armor, EquipmentSlotType.Chest), Is.True);

            CombatDamageResult result = CombatResolver.ApplyHit(
                new CombatHit(attacker, target, null, 100f, 10f, 0f, CombatAttackKind.Unarmed, Vector3.forward));

            Assert.That(result.ArmorRating, Is.EqualTo(1000f).Within(0.001f));
            Assert.That(result.Mitigation, Is.EqualTo(0.8f).Within(0.001f));
            Assert.That(result.FinalDamage, Is.EqualTo(20f).Within(0.001f));
            Assert.That(target.Health, Is.EqualTo(980f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(armorObject);
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [TestCase(0f)]
    [TestCase(-5f)]
    public void CombatResolver_ZeroOrNegativeBaseDamageDoesNotApplyDamage(float baseDamage)
    {
        GameObject attackerObject = new("Attacker");
        GameObject targetObject = new("Target");
        try
        {
            Combatant attacker = AddNpcCombatant(attackerObject, health: 100f, stamina: 100f);
            Combatant target = AddNpcCombatant(targetObject, health: 100f, stamina: 100f);

            CombatDamageResult result = CombatResolver.ApplyHit(
                new CombatHit(attacker, target, null, baseDamage, 10f, 0f, CombatAttackKind.Unarmed, Vector3.forward));

            Assert.That(result.Applied, Is.False);
            Assert.That(target.Health, Is.EqualTo(100f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void Combatant_ReadsWeaponFromEquipmentAndUsesWeaponDamageBeforeFallback()
    {
        GameObject actorObject = new("Sword Actor");
        GameObject weaponObject = new("Iron Sword");
        try
        {
            Combatant combatant = AddNpcCombatant(actorObject, health: 100f, stamina: 100f);
            Equipment equipment = actorObject.AddComponent<Equipment>();
            AddBone(actorObject, "RightHand");
            ItemComponent weapon = AddItem(
                weaponObject,
                ItemType.Weapon,
                equipBone: "RightHand",
                damage: 32f,
                weaponHanding: WeaponHanding.OneHanded);

            Assert.That(equipment.Equip(weapon, EquipmentSlotType.RightHand), Is.True);

            Assert.That(combatant.TryGetEquippedWeapon(out ItemComponent equippedWeapon, out EquipmentSlotType slot), Is.True);
            Assert.That(equippedWeapon, Is.SameAs(weapon));
            Assert.That(slot, Is.EqualTo(EquipmentSlotType.RightHand));
            Assert.That(combatant.GetAttackBaseDamage(equippedWeapon, fallbackDamage: 8f), Is.EqualTo(32f).Within(0.001f));
            Assert.That(combatant.GetAttackStaminaCost(equippedWeapon), Is.EqualTo(Combatant.DefaultOneHandedStaminaCost).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void Combatant_SumsEquippedArmorDefenseOncePerItem()
    {
        GameObject actorObject = new("Armored Actor");
        GameObject chestObject = new("Chestplate");
        GameObject helmObject = new("Helm");
        try
        {
            Combatant combatant = AddNpcCombatant(actorObject, health: 100f, stamina: 100f);
            Equipment equipment = actorObject.AddComponent<Equipment>();
            AddBone(actorObject, "Chest");
            AddBone(actorObject, "Head");
            ItemComponent chest = AddItem(chestObject, ItemType.Armor, equipBone: "Chest", defense: 20f);
            ItemComponent helm = AddItem(helmObject, ItemType.Armor, equipBone: "Head", defense: 15f);

            Assert.That(equipment.Equip(chest, EquipmentSlotType.Chest), Is.True);
            Assert.That(equipment.Equip(helm, EquipmentSlotType.Head), Is.True);

            Assert.That(combatant.GetArmorRating(), Is.EqualTo(35f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(chestObject);
            Object.DestroyImmediate(helmObject);
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void Combatant_SelectsUnarmedOneHandedAndTwoHandedStaminaCosts()
    {
        GameObject actorObject = new("Cost Actor");
        GameObject swordObject = new("Sword");
        GameObject greatswordObject = new("Greatsword");
        try
        {
            Combatant combatant = AddNpcCombatant(actorObject, health: 100f, stamina: 100f);
            ItemComponent sword = AddItem(swordObject, ItemType.Weapon, equipBone: "RightHand", damage: 12f, weaponHanding: WeaponHanding.OneHanded);
            ItemComponent greatsword = AddItem(greatswordObject, ItemType.Weapon, equipBone: "RightHand", damage: 20f, weaponHanding: WeaponHanding.TwoHanded);

            Assert.That(combatant.GetAttackStaminaCost(null), Is.EqualTo(Combatant.DefaultUnarmedStaminaCost).Within(0.001f));
            Assert.That(combatant.GetAttackStaminaCost(sword), Is.EqualTo(Combatant.DefaultOneHandedStaminaCost).Within(0.001f));
            Assert.That(combatant.GetAttackStaminaCost(greatsword), Is.EqualTo(Combatant.DefaultTwoHandedStaminaCost).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(swordObject);
            Object.DestroyImmediate(greatswordObject);
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void CombatAttackProfile_PowerAttackScalesDamageStaminaAndStagger()
    {
        GameObject attackerObject = new("Power Actor");
        GameObject targetObject = new("Target");
        GameObject weaponObject = new("War Axe");
        try
        {
            Combatant attacker = AddNpcCombatant(attackerObject, health: 100f, stamina: 100f);
            Combatant target = AddNpcCombatant(targetObject, health: 100f, stamina: 100f);
            Equipment equipment = attackerObject.AddComponent<Equipment>();
            AddBone(attackerObject, "RightHand");
            ItemComponent weapon = AddItem(
                weaponObject,
                ItemType.Weapon,
                equipBone: "RightHand",
                damage: 20f,
                weaponHanding: WeaponHanding.OneHanded);

            Assert.That(equipment.Equip(weapon, EquipmentSlotType.RightHand), Is.True);

            CombatHit hit = attacker.CreateMeleeHit(
                target,
                fallbackDamage: 8f,
                Vector3.forward,
                CombatAttackProfile.Power);

            Assert.That(hit.AttackStyle, Is.EqualTo(CombatAttackStyle.Power));
            Assert.That(hit.BaseDamage, Is.EqualTo(35f).Within(0.001f));
            Assert.That(hit.StaminaCost, Is.EqualTo(24f).Within(0.001f));
            Assert.That(hit.StaggerMultiplier, Is.EqualTo(1.5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(attackerObject);
            Object.DestroyImmediate(targetObject);
        }
    }

    [Test]
    public void BasicMeleeAttack_UsesStyleSpecificStaminaRequirements()
    {
        GameObject actorObject = new("Tired Puncher");
        try
        {
            NPCSoul soul = actorObject.AddComponent<NPCSoul>();
            soul.MaxHealth = 100f;
            soul.Health = 100f;
            soul.MaxStamina = 100f;
            soul.Stamina = 12f;
            BasicMeleeAttack attack = actorObject.AddComponent<BasicMeleeAttack>();

            Assert.That(attack.CanStartAttack(CombatAttackStyle.Light), Is.True);
            Assert.That(attack.CanStartAttack(CombatAttackStyle.Power), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void CombatReadiness_GatesMeleeUntilReadyCompletes()
    {
        GameObject actorObject = new("Readying Puncher");
        try
        {
            NPCSoul soul = actorObject.AddComponent<NPCSoul>();
            soul.MaxHealth = 100f;
            soul.Health = 100f;
            soul.MaxStamina = 100f;
            soul.Stamina = 100f;
            BasicMeleeAttack attack = actorObject.AddComponent<BasicMeleeAttack>();
            CombatReadiness readiness = actorObject.AddComponent<CombatReadiness>();

            Assert.That(attack.CanStartAttack(), Is.False);
            Assert.That(readiness.RequestReady(), Is.False);
            Assert.That(attack.CanStartAttack(), Is.False);

            readiness.OnCombatReadyComplete();

            Assert.That(readiness.CanAttack, Is.True);
            Assert.That(attack.CanStartAttack(), Is.True);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void CombatReadiness_SheathingBlocksMeleeAndReturnsToRelaxed()
    {
        GameObject actorObject = new("Sheathing Puncher");
        try
        {
            NPCSoul soul = actorObject.AddComponent<NPCSoul>();
            soul.MaxHealth = 100f;
            soul.Health = 100f;
            soul.MaxStamina = 100f;
            soul.Stamina = 100f;
            BasicMeleeAttack attack = actorObject.AddComponent<BasicMeleeAttack>();
            CombatReadiness readiness = actorObject.AddComponent<CombatReadiness>();

            readiness.RequestReady();
            readiness.OnCombatReadyComplete();
            Assert.That(attack.CanStartAttack(), Is.True);

            readiness.RequestSheathe();
            Assert.That(attack.CanStartAttack(), Is.False);

            readiness.OnCombatSheatheComplete();
            Assert.That(readiness.IsRelaxed, Is.True);
            Assert.That(attack.CanStartAttack(), Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void BasicMeleeAttack_DamagesHostileNpcWhenTargetIsOverlappingAttacker()
    {
        GameObject playerObject = new("Player");
        GameObject npcObject = new("Hostile NPC");
        try
        {
            PlayerSoul playerSoul = playerObject.AddComponent<PlayerSoul>();
            playerSoul.MaxHealth = 100f;
            playerSoul.Health = 100f;
            playerSoul.MaxStamina = 100f;
            playerSoul.Stamina = 100f;

            BasicMeleeAttack attack = playerObject.AddComponent<BasicMeleeAttack>();
            NPCSoul npcSoul = npcObject.AddComponent<NPCSoul>();
            npcSoul.MaxHealth = 100f;
            npcSoul.Health = 100f;
            npcSoul.IsHostile = true;
            npcObject.AddComponent<BoxCollider>();

            playerObject.transform.position = Vector3.zero;
            playerObject.transform.rotation = Quaternion.identity;
            npcObject.transform.position = Vector3.zero;
            Physics.SyncTransforms();

            Assert.That(attack.TryBeginAttack(), Is.True);
            attack.ResolveHitFrame();

            Assert.That(npcSoul.Health, Is.LessThan(100f));
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
            Object.DestroyImmediate(playerObject);
        }
    }

    [Test]
    public void CombatWeaponPresentation_OneHandedWeaponMovesBetweenHipAndHand()
    {
        GameObject actorObject = new("Sword Actor");
        GameObject weaponObject = new("Iron Sword");
        try
        {
            actorObject.AddComponent<Combatant>();
            Equipment equipment = actorObject.AddComponent<Equipment>();
            Transform rightHand = AddBone(actorObject, "RightHand");
            Transform rightHip = AddBone(actorObject, "RightHip");
            ItemComponent weapon = AddItem(
                weaponObject,
                ItemType.Weapon,
                equipBone: "RightHand",
                damage: 12f,
                weaponHanding: WeaponHanding.OneHanded);

            Assert.That(equipment.Equip(weapon, EquipmentSlotType.RightHand), Is.True);

            CombatWeaponPresentation presentation = actorObject.AddComponent<CombatWeaponPresentation>();

            Assert.That(presentation.PresentSheathed(), Is.True);
            Assert.That(weapon.transform.parent, Is.SameAs(rightHip));

            Assert.That(presentation.PresentUnsheathed(), Is.True);
            Assert.That(weapon.transform.parent, Is.SameAs(rightHand));
        }
        finally
        {
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void CombatWeaponPresentation_TwoHandedWeaponMovesBetweenBackAndHand()
    {
        GameObject actorObject = new("Greatsword Actor");
        GameObject weaponObject = new("Greatsword");
        try
        {
            actorObject.AddComponent<Combatant>();
            Equipment equipment = actorObject.AddComponent<Equipment>();
            Transform rightHand = AddBone(actorObject, "RightHand");
            AddBone(actorObject, "LeftHand");
            Transform rightBack = AddBone(actorObject, "RightBack");
            ItemComponent weapon = AddItem(
                weaponObject,
                ItemType.Weapon,
                equipBone: "RightHand",
                damage: 20f,
                weaponHanding: WeaponHanding.TwoHanded);

            Assert.That(equipment.Equip(weapon, EquipmentSlotType.RightHand), Is.True);

            CombatWeaponPresentation presentation = actorObject.AddComponent<CombatWeaponPresentation>();

            Assert.That(presentation.PresentSheathed(), Is.True);
            Assert.That(weapon.transform.parent, Is.SameAs(rightBack));

            Assert.That(presentation.PresentUnsheathed(), Is.True);
            Assert.That(weapon.transform.parent, Is.SameAs(rightHand));
        }
        finally
        {
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void CombatWeaponPresentation_DoesNotReplaceUnequipOrDropSemantics()
    {
        GameObject actorObject = new("Drop Actor");
        GameObject weaponObject = new("Sword");
        try
        {
            actorObject.AddComponent<Combatant>();
            Equipment equipment = actorObject.AddComponent<Equipment>();
            AddBone(actorObject, "RightHand");
            AddBone(actorObject, "RightHip");
            ItemComponent weapon = AddItem(
                weaponObject,
                ItemType.Weapon,
                equipBone: "RightHand",
                damage: 12f,
                weaponHanding: WeaponHanding.OneHanded);

            Assert.That(equipment.Equip(weapon, EquipmentSlotType.RightHand), Is.True);
            Assert.That(actorObject.AddComponent<CombatWeaponPresentation>().PresentSheathed(), Is.True);

            Assert.That(equipment.UnequipItem(weapon), Is.True);
            Assert.That(equipment.IsEquipped(weapon), Is.False);
            Assert.That(weapon.transform.parent, Is.Null);
            Assert.That(weapon.gameObject.activeSelf, Is.False);

            weapon.gameObject.SetActive(true);
            Assert.That(equipment.Equip(weapon, EquipmentSlotType.RightHand), Is.True);
            Assert.That(equipment.DetachForDrop(weapon), Is.SameAs(weapon));
            Assert.That(equipment.IsEquipped(weapon), Is.False);
            Assert.That(weapon.transform.parent, Is.Null);
            Assert.That(weapon.gameObject.activeSelf, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(weaponObject);
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void FishingState_CombatToggleIsBlockedOnlyWhileFishingNeedsInput()
    {
        GameObject actorObject = new("Fishing Actor");
        try
        {
            Sol.Fishing.FishingState fishingState = actorObject.AddComponent<Sol.Fishing.FishingState>();

            SetPrivateField(fishingState, "_isRodEquipped", true);
            Assert.That(fishingState.ShouldBlockCombatToggle, Is.False);

            SetPrivateField(fishingState, "_isCastPending", true);
            Assert.That(fishingState.ShouldBlockCombatToggle, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    [Test]
    public void HitReaction_ReportsActiveReactionDuringFallbackWindow()
    {
        GameObject npcObject = new("Reacting NPC");
        try
        {
            HitReaction reaction = npcObject.AddComponent<HitReaction>();

            reaction.PlayHitReaction(Vector3.forward);

            Assert.That(reaction.IsReacting, Is.True);
        }
        finally
        {
            Object.DestroyImmediate(npcObject);
        }
    }

    [Test]
    public void ActionSystem_DispatchImmediateReportsWhetherInstantActionStarted()
    {
        GameObject actionSystemObject = new("Action System");
        GameObject actorObject = new("Actor");
        try
        {
            ActionSystem actionSystem = actionSystemObject.AddComponent<ActionSystem>();
            InstantTestAction accepted = new(canExecute: true);
            InstantTestAction rejected = new(canExecute: false);

            Assert.That(actionSystem.DispatchImmediate(accepted, actorObject), Is.True);
            Assert.That(accepted.Started, Is.True);
            Assert.That(actionSystem.DispatchImmediate(rejected, actorObject), Is.False);
            Assert.That(rejected.Started, Is.False);
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
            Object.DestroyImmediate(actionSystemObject);
        }
    }

    [Test]
    public void Combatant_UnarmedFallbackDamageWorksAndRejectsInsufficientStamina()
    {
        GameObject actorObject = new("Tired Actor");
        try
        {
            Combatant combatant = AddNpcCombatant(actorObject, health: 100f, stamina: 5f);

            Assert.That(combatant.GetAttackBaseDamage(null, fallbackDamage: 9f), Is.EqualTo(9f).Within(0.001f));
            Assert.That(combatant.CanSpendStamina(combatant.GetAttackStaminaCost(null)), Is.False);
            Assert.That(combatant.TrySpendStamina(combatant.GetAttackStaminaCost(null)), Is.False);
            Assert.That(combatant.Stamina, Is.EqualTo(5f).Within(0.001f));
        }
        finally
        {
            Object.DestroyImmediate(actorObject);
        }
    }

    private static Combatant AddNpcCombatant(GameObject actorObject, float health, float stamina)
    {
        NPCSoul soul = actorObject.AddComponent<NPCSoul>();
        soul.MaxHealth = health;
        soul.Health = health;
        soul.MaxStamina = 100f;
        soul.Stamina = stamina;

        Combatant combatant = actorObject.AddComponent<Combatant>();
        combatant.RefreshReferences();
        return combatant;
    }

    private static ItemComponent AddItem(
        GameObject itemObject,
        ItemType itemType,
        string equipBone,
        float damage = 0f,
        float defense = 0f,
        WeaponHanding weaponHanding = WeaponHanding.OneHanded)
    {
        itemObject.AddComponent<BoxCollider>();
        itemObject.AddComponent<GrabbableComponent>();
        ItemComponent item = itemObject.AddComponent<ItemComponent>();

        SerializedObject serializedObject = new(item);
        serializedObject.Update();
        serializedObject.FindProperty("_itemId").stringValue = string.Empty;
        serializedObject.FindProperty("_itemName").stringValue = itemObject.name;
        serializedObject.FindProperty("_itemType").enumValueIndex = (int)itemType;
        serializedObject.FindProperty("_damage").floatValue = damage;
        serializedObject.FindProperty("_defense").floatValue = defense;
        serializedObject.FindProperty("_equipBone").stringValue = equipBone;
        serializedObject.FindProperty("_weaponHanding").enumValueIndex = (int)weaponHanding;
        serializedObject.ApplyModifiedPropertiesWithoutUndo();

        return item;
    }

    private static Transform AddBone(GameObject actorObject, string boneName)
    {
        GameObject bone = new(boneName);
        bone.transform.SetParent(actorObject.transform);
        bone.transform.localPosition = Vector3.zero;
        bone.transform.localRotation = Quaternion.identity;
        return bone.transform;
    }

    private static void SetPrivateField(object target, string fieldName, object value)
    {
        System.Reflection.FieldInfo field = target.GetType().GetField(
            fieldName,
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null);
        field.SetValue(target, value);
    }

    private sealed class InstantTestAction : GameAction
    {
        private readonly bool _canExecute;

        public InstantTestAction(bool canExecute)
        {
            _canExecute = canExecute;
        }

        public bool Started { get; private set; }

        public override bool CanExecute() => _canExecute;

        public override void OnStart()
        {
            Started = true;
            Complete();
        }
    }
}
