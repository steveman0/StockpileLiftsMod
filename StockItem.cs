using UnityEngine;

public class StockItem
{
    public ItemBase Item;
    public int StockLimit;

    public StockItem(ItemBase item, int stocklimit)
    {
        this.Item = item;
        this.StockLimit = stocklimit;
    }
}

