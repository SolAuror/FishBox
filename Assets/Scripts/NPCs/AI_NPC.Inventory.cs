namespace Sol.AI
{
    /// <summary>
    /// AI_NPC partial - the simplified AI only keeps an inventory reference.
    /// </summary>
    public partial class AI_NPC
    {
        private Sol.Inventory inventory;

        public Sol.Inventory Inventory => inventory;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public string LastItemAction => string.Empty;
#endif

        private void InitInventory()
        {
            inventory = GetComponent<Sol.Inventory>();
        }
    }
}
