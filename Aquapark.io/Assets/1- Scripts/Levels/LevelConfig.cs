using Dreamteck.Splines;
using UnityEngine;

/// <summary>
/// Sits on the root of every generated level prefab. Holds the references and race settings a level loader needs.
/// </summary>
public class LevelConfig : MonoBehaviour
{
    [Header("Info")]
    public int levelNumber;
    public float trackLength;

    [Header("References")]
    public SplineComputer mainSpline;
    public SplineComputer endSpline;
    public Transform finishLine;
    public Transform endCamAnchor;

    [Header("Race")]
    public float playerSpeed = 25f;
    public float aiSpeedMin = 23f;
    public float aiSpeedMax = 30f;
    [Tooltip("Gap between racers at the start, as a fraction of the spline (0.0045 = 0.45%).")]
    public float aiStartSpacingPercent = 0.0045f;
    public float startBoostDuration = 5f;
}
