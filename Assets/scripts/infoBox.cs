using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class infoBox : MonoBehaviour
{
    private float remainingTime = 0.5f;
    // Start is called before the first frame update
    

    // Update is called once per frame
    void Update()
    {
        if(remainingTime>0) remainingTime -= Time.deltaTime;
        if (Input.GetKeyDown(KeyCode.Space)&& remainingTime<0)
        {
            Destroy(gameObject);
        }
    }
}
