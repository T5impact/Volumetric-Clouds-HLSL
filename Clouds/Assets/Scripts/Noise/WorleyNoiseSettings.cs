using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Worley Noise Settings", menuName = "Noise/WorleyNoise")]
public class WorleyNoiseSettings : ScriptableObject
{
    public int seed;
    [Range(1, 60)]
    public int octave1Divisions;
    [Range(1, 60)]
    public int octave2Divisions;
    [Range(1, 60)]
    public int octave3Divisions;

    public float persistance;
    public float lacunarity;
    public float scale;
    public Vector3 offset;
    public bool invert;
}
