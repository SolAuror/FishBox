namespace Sol.Rpg
{
    public interface IActorVitals
    {
        float Health { get; set; }
        float MaxHealth { get; set; }
        float Stamina { get; set; }
        float MaxStamina { get; set; }
        bool IsAlive { get; }

        void TakeDamage(float amount);
        void Heal(float amount);
        bool SpendStamina(float amount);
        void RestoreStamina(float amount);
    }
}
