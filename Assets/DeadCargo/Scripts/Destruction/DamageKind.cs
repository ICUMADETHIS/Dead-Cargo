namespace DeadCargo.Destruction
{
    /// <summary>What caused damage. Destructibles use it to scale the effect: a mining beam cuts cleanly, a missile blasts.</summary>
    public enum DamageKind
    {
        /// <summary>Guns, beams and other direct weapon hits.</summary>
        Weapon,

        /// <summary>Blasts with a radius, such as missiles and mines.</summary>
        Explosion,

        /// <summary>Mining tools: precise cuts whose material is meant to be collected.</summary>
        Mining,

        /// <summary>Collisions.</summary>
        Impact,

        /// <summary>Debug tools and scripted events.</summary>
        Script
    }
}
