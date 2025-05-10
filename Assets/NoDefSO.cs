using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "NoDefSO", menuName = "")]
public class NoDefSO : ScriptableObject
{
    public string TestString;
    public Shader TestShader;
    public Material TestMaterial;
    public Camera TestCamera;
    public Color TestColor;
    public Color TestColor2;
}