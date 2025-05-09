using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "Type1", menuName = "")]
public class TestScriptableObject : TestScriptableObjectAbstract
{
   [SerializeField]
   private float FloatSerializedField;
   private float floatField;
   public float FloatPublicField;
   public float FloatProp { get; set; }

   public int Integer;
   public Color Color;
   public double DoubleField;
   public string StringField;
   public GameObject GameObjectField;
}
