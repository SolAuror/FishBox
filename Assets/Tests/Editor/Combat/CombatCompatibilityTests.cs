using NUnit.Framework;
using Sol;
using Sol.AI;
using Sol.Combat;
using Sol.Grab;
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
}
