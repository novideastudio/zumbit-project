using System;
using UnityEngine;
[CreateAssetMenu(menuName = "Zombit/Wave")]
public class WaveData : ScriptableObject
{
    [Serializable] public class Group { public GameObject enemyPrefab; public int count = 10; public float interval = 0.6f; }
    public Group[] groups;
}
