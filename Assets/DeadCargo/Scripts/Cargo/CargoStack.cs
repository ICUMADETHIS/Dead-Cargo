namespace DeadCargo.Cargo
{
    /// <summary>A quantity of one cargo item.</summary>
    public readonly struct CargoStack
    {
        public CargoStack(CargoItemDefinition item, int quantity)
        {
            Item = item;
            Quantity = quantity;
        }

        public CargoItemDefinition Item { get; }
        public int Quantity { get; }

        public float Mass => Item.UnitMass * Quantity;
        public int Value => Item.UnitValue * Quantity;
    }
}
