namespace DeadCargo.Visuals
{
    /// <summary>Property names of the DeadCargo/Space Sky shader, shared by its inspector, baker and setup.</summary>
    public static class SpaceSkyProperties
    {
        public const string ShaderName = "DeadCargo/Space Sky";

        public const string Rotation = "_SkyRotation";
        public const string LightDirection = "_LightDirection";
        public const string Exposure = "_Exposure";

        public const string NebulaFlow = "_NebulaFlow";
        public const string NebulaFlowSpeed = "_NebulaFlowSpeed";
        public const string FarDrift = "_FarDrift";
        public const string StarTwinkle = "_StarTwinkle";
        public const string TwinkleSpeed = "_TwinkleSpeed";

        public const string FarNebula = "_FarNebula";
        public const string FarNebulaColor = "_FarNebulaColor";
        public const string FarNebulaThreshold = "_FarNebulaThreshold";
        public const string FarNebulaRotation = "_FarNebulaRotation";
        public const string GalaxyColor = "_GalaxyColor";
        public const string Galaxies = "_Galaxies";

        public const string DustLanes = "_DustLanes";
        public const string DustThreshold = "_DustThreshold";
        public const string DustRotation = "_DustRotation";
        public const string NebulaSoftness = "_NebulaSoftness";

        public const string InkColor = "_InkColor";
        public const string InkWidth = "_InkWidth";
        public const string CloudLighting = "_CloudLighting";
        public const string CloudRelief = "_CloudRelief";
        public const string ShadowTint = "_ShadowTint";
        public const string HighlightColor = "_HighlightColor";
        public const string HighlightLevel = "_HighlightLevel";
        public const string Hatching = "_Hatching";
        public const string HatchLines = "_HatchLines";
        public const string HatchWidth = "_HatchWidth";
        public const string StarSparkle = "_StarSparkle";

        public const string VoidColor = "_VoidColor";
        public const string PlaneColor = "_PlaneColor";
        public const string BackdropBands = "_BackdropBands";
        public const string BackdropWidth = "_BackdropWidth";
        public const string BackdropWobble = "_BackdropWobble";

        public const string NebulaMap = "_NebulaMap";
        public const string NebulaOpacity = "_NebulaOpacity";
        public const string NebulaThresholds = "_NebulaThresholds";
        public const string NebulaHueSplit = "_NebulaHueSplit";
        public const string NebulaStarDimming = "_NebulaStarDimming";
        public const string NebulaColorA0 = "_NebulaColorA0";
        public const string NebulaColorA1 = "_NebulaColorA1";
        public const string NebulaColorA2 = "_NebulaColorA2";
        public const string NebulaColorB0 = "_NebulaColorB0";
        public const string NebulaColorB1 = "_NebulaColorB1";
        public const string NebulaColorB2 = "_NebulaColorB2";

        public const string StarSeed = "_StarSeed";
        public const string StarPlaneBoost = "_StarPlaneBoost";
        public const string StarCoolColor = "_StarCoolColor";
        public const string StarWarmColor = "_StarWarmColor";
        public const string StarTints = "_StarTints";
        public const string StarSmall = "_StarSmall";
        public const string StarMedium = "_StarMedium";
        public const string StarLarge = "_StarLarge";

        public const string NebulaSeed = "_NebulaSeed";
        public const string NebulaScale = "_NebulaScale";
        public const string NebulaOctaves = "_NebulaOctaves";
        public const string NebulaSwirl = "_NebulaSwirl";
        public const string NebulaBandWidth = "_NebulaBandWidth";
        public const string NebulaBandFocus = "_NebulaBandFocus";
        public const string NebulaHueScale = "_NebulaHueScale";
        public const string NebulaBakeHash = "_NebulaBakeHash";

        /// <summary>Seeds are stored as floats, which hold whole numbers exactly up to this size.</summary>
        public const int MaxSeed = 65535;
    }
}
