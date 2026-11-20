using System.Collections;
using UnityEngine;

public class ProximityChildActivator : MonoBehaviour
{
    private bool currentState = false;

    private IEnumerator Start()
    {
        int childCount = transform.childCount;
        for (int i = 0; i < childCount; i++)
        {
            transform.GetChild(i).gameObject.SetActive(false);
        }
        yield return new WaitForSeconds(Random.Range(0f, 3f));
        WaitForSeconds wait = new WaitForSeconds(3f);

        while (true)
        {
            var player = ENT_Player.GetPlayer();
            if (player != null)
            {
                bool isNear = (player.gameObject.transform.position - transform.position).sqrMagnitude <= 10000f;
                if (isNear != currentState)
                {
                    currentState = isNear;
                    childCount = transform.childCount;
                    for (int i = 0; i < childCount; i++)
                    {
                        transform.GetChild(i).gameObject.SetActive(currentState);
                    }
                }
            }
            yield return wait;
        }
    }
}