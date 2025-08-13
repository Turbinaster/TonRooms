using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

public class Lift : MonoBehaviour
{
    int floor = 0, floors;
    Transform platform;
    bool up = false, started = false;
    CharacterController cc;

    private void Start()
    {
        platform = transform.parent;
    }

    public void StartLift()
    {
        if (!started)
        {
            started = true;
            floors = platform.parent.GetComponent<HexCell>().floors;
            StartCoroutine(LiftWork());
        }
    }

    IEnumerator LiftWork()
    {
        yield return new WaitForSeconds(5);
        if (floor == 0) up = true;
        if (floor == floors) up = false;
        if (up) floor++; else floor--;
        float y = 5 * floor + 0.4f;
        StartCoroutine(AnimateMove(platform, platform.localPosition, new Vector3(platform.localPosition.x, y, platform.localPosition.z), 5));
    }

    IEnumerator AnimateMove(Transform t, Vector3 origin, Vector3 target, float duration)
    {
        if (cc != null) cc.enabled = false;
        float journey = 0f;
        while (journey <= duration)
        {
            journey = journey + Time.deltaTime;
            float percent = Mathf.Clamp01(journey / duration);

            t.localPosition = Vector3.Lerp(origin, target, percent);

            yield return null;
        }
        if (cc != null) cc.enabled = true;
        StartCoroutine(LiftWork());
    }



    void OnTriggerEnter(Collider other)
    {
        var player = other.GetComponent<PlayerBehaviour>();
        if (player != null)
        {
            player.transform.parent = platform;
            cc = player.GetComponent<CharacterController>();
        }
    }

    void OnTriggerExit(Collider other)
    {
        var player = other.GetComponent<PlayerBehaviour>();
        if (player != null)
        {
            player.transform.parent = null;
            cc = null;
        }
    }
}
