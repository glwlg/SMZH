namespace XTD.Content
{
    public enum Faction
    {
        Player,
        Enemy
    }

    public enum HeroClassType
    {
        BorderCommander,
        SpiritSummoner,
        ThunderMage,
        TalismanSealer
    }

    public enum UnitRole
    {
        Soldier,
        Elite,
        Hero,
        Structure,
        Monster,
        Boss
    }

    public enum CardType
    {
        Soldier,
        EliteSoldier,
        Hero,
        Spell,
        Tactic,
        Debuff,
        Structure,
        Economy,
        Curse
    }

    public enum CardReleaseRule
    {
        None,
        PlayerSide,
        Anywhere
    }

    public enum CardRarity
    {
        Common,
        Uncommon,
        Rare,
        Epic,
        Legendary
    }

    public enum EffectType
    {
        None,
        Damage,
        AreaDamage,
        Heal,
        Shield,
        BuffAttack,
        BuffAttackSpeed,
        Slow,
        Stun,
        Knockback,
        Burn,
        Poison,
        DrawCard,
        GainMana,
        GainMorale,
        GainGold
    }

    public enum TargetRule
    {
        None,
        EnemyFrontline,
        EnemyBackline,
        AllEnemies,
        FriendlyFrontline,
        AllFriendlyUnits,
        PlayerBase,
        EnemyBase,
        Self
    }

    public enum ArtifactRarity
    {
        Common,
        Rare,
        Epic,
        Legendary
    }

    public enum ArtifactTrigger
    {
        Passive,
        BattleStart,
        CardPlayed,
        UnitSummoned,
        UnitDied,
        RewardGranted,
        ShopOpened
    }

    public enum MapNodeType
    {
        NormalMonster,
        EliteMonster,
        Shop,
        Rest,
        Opportunity,
        Mystery,
        Artifact,
        SmallBoss,
        FinalBoss
    }

    public enum FloorAffixType
    {
        None,
        DemonFog,
        ThunderTribulation,
        DemonTide,
        ImmortalArray
    }

    public enum EncounterPressurePattern
    {
        None,
        VanguardRush,
        BacklineVolley,
        ShieldStandard
    }
}
