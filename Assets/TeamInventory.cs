using UnityEngine;
using TMPro;

public class TeamInventory : MonoBehaviour
{
    [SerializeField] private TMP_Text woodLabel;
    [SerializeField] private TMP_Text fundsLabel;
    private int woodCount = 0;
    private int funds = 500;

    public void AddWood(int amount)
    {
        woodCount += amount;
        woodLabel.text = $"Wood: {woodCount}";
    }

    public void SellOneWood()
    {
        if(woodCount < 1) return ;
        funds += 2;
        woodCount -= 1;
        woodLabel.text = $"Wood: {woodCount}";
        fundsLabel.text = $"Funds: {funds}";
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        fundsLabel.text = $"Funds: {funds}";
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
