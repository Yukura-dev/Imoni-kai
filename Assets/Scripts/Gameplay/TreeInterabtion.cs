using System;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TreeInterabtion : MonoBehaviour
{
    [SerializeField] private RectTransform player;
    [SerializeField] private TeamInventory inventory;
    private bool isMoving = false;

    private int remainingHarvests = 3;
    public void OnTreeClicked()
    {
        if(remainingHarvests == 0 || IsInvoking(nameof(HarvestWood)))
        {
            return;
        }
        isMoving = true;
    }
    private void HarvestWood()
    {
        inventory.AddWood(3);
        remainingHarvests -= 1;
        Debug.Log($"remain={remainingHarvests}");
        if (remainingHarvests > 0)
        {
            Invoke(nameof(HarvestWood), 1.6f);
        }
        else
        {
            GetComponent<Button>().interactable = false;
            Invoke(nameof(Regrow), 40f);
        }
    }
    private void Regrow()
    {
        remainingHarvests = 3;
        GetComponent<Button>().interactable = true;
    }

    // Update is called once per frame
    void Update()
    {
        if(!isMoving)return;
        Vector2 target = new Vector2(-70f,0f);
        player.anchoredPosition = Vector2.MoveTowards(
            player.anchoredPosition, target, 140f * Time.deltaTime);
        if (player.anchoredPosition == target)
        {
            isMoving = false;
            if (!IsInvoking(nameof(HarvestWood)))
                Invoke(nameof(HarvestWood), 1.6f);
        }
    }
}
