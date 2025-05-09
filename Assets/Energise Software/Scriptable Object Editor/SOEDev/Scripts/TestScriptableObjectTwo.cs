using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Type2", menuName = "")]
public class TestScriptableObjectTwo : ScriptableObject
{
    public string TestString;
    public Shader TestShader;
    public Material TestMaterial;
    public Camera TestCamera;
    public Color TestColor;
}
