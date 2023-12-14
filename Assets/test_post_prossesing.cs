using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteInEditMode]
public class test_post_prossesing : MonoBehaviour
{
    public Material postProcessingMat;

    private void OnRenderImage(RenderTexture src, RenderTexture dest) {
        Graphics.Blit(src,dest,postProcessingMat);
    }
}
