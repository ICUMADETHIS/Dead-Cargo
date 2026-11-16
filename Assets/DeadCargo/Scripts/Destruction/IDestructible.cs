namespace DeadCargo.Destruction
{
    /// <summary>
    /// Anything that reacts to damage by changing shape: carving voxels, shattering, losing parts.
    /// Weapons reach it through <see cref="DestructionDamageReceiver"/> on its colliders; other systems call it directly.
    /// </summary>
    public interface IDestructible
    {
        void ApplyDamage(in DamageEvent damage);
    }
}
