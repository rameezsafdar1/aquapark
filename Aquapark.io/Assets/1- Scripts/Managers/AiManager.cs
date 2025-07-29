using Dreamteck.Splines;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;
using TMPro;

public class AiManager : MonoBehaviour
{
    [SerializeField] private Locomotion Player;
    [SerializeField] private PlayerEffects effects;
    [SerializeField] private SplineFollower[] allAgents;
    [SerializeField] private float minSpeed, maxSpeed, startSpacing, startBoostDuration = 5;
    [SerializeField] private TextMeshPro playerPosition;
    private List<double> distances = new List<double>();
    private float startPercent, boostDuration, initialSpeed;
    private bool playerBoostTime;

    private void Awake()
    {
        for (int i = 0; i < allAgents.Length; i++)
        {
            allAgents[i].followSpeed = Random.Range(minSpeed, maxSpeed);
        }
    }

    private void Start()
    {
        InitAi();
    }

    private void Update()
    {
        Positioning();
        if (playerBoostTime)
        {
            boostDuration += Time.deltaTime;
            if (boostDuration >= startBoostDuration)
            {
                Player.splineFollower.followSpeed = initialSpeed;
                playerBoostTime = false;
                effects.windLines.SetActive(false);
            }
        }
    }

    public void InitAi()
    {
        int playerPos = Random.Range(0, allAgents.Length);

        for (int i = 0; i < allAgents.Length; i++)
        {
            startPercent += startSpacing;
            if (i == playerPos)
            {
                //allAgents[i].SetPercent(Player.GetPercent() + 0.003f);
                Player.splineFollower.SetPercent(startPercent);
            }
            else
            {
                allAgents[i].SetPercent(startPercent);
            }
        }
    }

    public void StartGame()
    {
        Player.ShakeComplete();
        Player.splineFollower.follow = true;
        initialSpeed = Player.splineFollower.followSpeed;
        Player.splineFollower.followSpeed += 20;
        playerBoostTime = true;
        effects.windLines.SetActive(true);

        for (int i = 0; i < allAgents.Length; i++)
        {
            allAgents[i].follow = true;
        }
    }

    public void SetAiDynamically()
    {
        for (int i = 0; i < allAgents.Length; i++)
        {
            if (allAgents[i].GetPercent() < Player.splineFollower.GetPercent())
            {
                allAgents[i].SetPercent(Player.splineFollower.GetPercent() + Random.Range(-0.015f, -0.003f));
            }

            allAgents[i].followSpeed = Random.Range(minSpeed, maxSpeed);
        }
    }

    private void Positioning()
    {
        distances.Clear();

        for (int i = 0; i < allAgents.Length; i++)
        {
            distances.Add(allAgents[i].GetPercent());
        }

        double playerPercent = Player.splineFollower.GetPercent();
        distances.Add(playerPercent);

        var sorted = distances.OrderByDescending(d => d).ToList();

        int rank = sorted.IndexOf(playerPercent) + 1;
        playerPosition.text = GetOrdinal(rank);
    }

    string GetOrdinal(int number)
    {
        if (number % 100 >= 11 && number % 100 <= 13) return number + "th";
        switch (number % 10)
        {
            case 1: return number + "st";
            case 2: return number + "nd";
            case 3: return number + "rd";
            default: return number + "th";
        }
    }
}
