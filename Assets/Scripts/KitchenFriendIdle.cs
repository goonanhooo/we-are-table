using UnityEngine;
/// <summary>Very small, unhurried motions for the friends gathered outside the kitchen.</summary>
public class KitchenFriendIdle : MonoBehaviour
{
    public float lift=.025f, sway=.7f, speed=.65f, phase;
    Vector3 p;Quaternion q;
    void Awake(){p=transform.localPosition;q=transform.localRotation;}
    void Update(){float t=Time.time*speed+phase;transform.localPosition=p+Vector3.up*(Mathf.Sin(t)*lift);transform.localRotation=q*Quaternion.Euler(0,Mathf.Sin(t*.7f)*sway,0);}
}
