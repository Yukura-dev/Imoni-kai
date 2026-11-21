using UnityEngine;
using TMPro;

public class TeamInventory : MonoBehaviour
{
    [SerializeField] private TMP_Text woodLabel;
    private int woodCount = 0;

    public void AddWood(int amount)
    {
        woodCount += amount;
        woodLabel.text = $"Wood: {woodCount}";
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
