using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Jobs;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;

[RequireComponent(typeof(CharacterController))]
public class PlayerControl : MonoBehaviour
{
    private Camera cam;
    public float walkingSpeed = 7.5f;
    public float runningSpeed = 11.5f;
    public float jumpSpeed = 8.0f;
    public float gravity = 20.0f;
    public float lookSpeed = 2.0f;
    public float lookXLimit = 45.0f;

    public float flenght = 50f;
    public float fnumber = 1.8f;
    public float focusdistance = 3000f;
    public float coc = 0.03f;
    public float apertureDiameter = 4.0f;
    public float sensorSizeHorizontal = 24f;
    public float sensorSizevertikal = 35f;
    public RawImage rawImageOutput = null;

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    [HideInInspector]
    public bool canMove = true;

    //todo: privet serializ
    public float viewMaxDistance = 20f;
    public float angleOfViewHorizontal = 16f;
    public float angleOfViewVertical = 24f;
    public int sizeOfTheFilterHorizontal = 10;
    public int sizeOfTheFilterVertical = 10;

    //
    private NativeArray<RaycastCommand> _raycastCommands;
    private NativeArray<RaycastHit> _raycastHits;
    private JobHandle _jobHandle;
    private bool isNextRaycast = false;


    void Awake()
    {
        int size = sizeOfTheFilterVertical * sizeOfTheFilterHorizontal;
        _raycastCommands = new NativeArray<RaycastCommand>(size, Allocator.Persistent);
        _raycastHits = new NativeArray<RaycastHit>(size, Allocator.Persistent);
    }

    private void OnDestroy()
    {
        _jobHandle.Complete();
        _raycastCommands.Dispose();
        _raycastHits.Dispose();
    }
    void CompliteRayCast()
    {
        isNextRaycast = false;
        //valtozok
        float h = flenght + (flenght * flenght) / (fnumber * coc);
        float dofFarMax = (h * focusdistance) / (h - (focusdistance - flenght));
        float dofNearMin = (h * focusdistance) / (h + (focusdistance - flenght));
        float dof = dofFarMax - dofNearMin;
        float f2 = flenght * flenght;
        //float dof= (2*focusdistance*focusdistance*fnumber*coc)/(flenght*flenght);
        Debug.Log(dofFarMax);
        Debug.Log(dofNearMin);
        Debug.Log(dof);

        float pixelpermmx = sizeOfTheFilterHorizontal / sensorSizeHorizontal;
        float pixelpermmy = sizeOfTheFilterVertical / sensorSizevertikal;
        //adat feldolgozása
        Color[] rawColorImage = new Color[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        float[] depthImage = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];

        for (int i = 0; i < sizeOfTheFilterVertical; i++) {
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++) {
                if (_raycastHits[i * sizeOfTheFilterVertical + j].collider != null) {
                    Renderer rend = _raycastHits[i * sizeOfTheFilterVertical + j].transform.GetComponent<Renderer>();
                    MeshCollider meshCollider = _raycastHits[i * sizeOfTheFilterVertical + j].collider as MeshCollider;
                    if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
                    {
                        string problem = "probléma: ";
                        if (rend == null) problem += "nincs renderer\n";
                        if (rend.sharedMaterial == null) problem += "nincs shader material\n";
                        if (rend.sharedMaterial.mainTexture == null) problem += "nincs mainTextura\n";
                        if (meshCollider == null) problem += "nincs meshCollider\n";
                        Debug.Log(problem);
                        return;
                    }
                    Texture2D tex = rend.material.mainTexture as Texture2D;
                    Vector2 pixelUV = _raycastHits[i * sizeOfTheFilterVertical + j].textureCoord;
                    pixelUV.x *= tex.width;
                    pixelUV.y *= tex.height;
                    rawColorImage[i * sizeOfTheFilterVertical + j] = tex.GetPixel((int)pixelUV.x, (int)pixelUV.y);
                    depthImage[i * sizeOfTheFilterVertical + j] = (_raycastHits[i * sizeOfTheFilterVertical + j].distance)*1000;
                }
                else
                {
                    rawColorImage[i * sizeOfTheFilterVertical + j] = Color.blue;
                    depthImage[i * sizeOfTheFilterVertical + j] = dofFarMax;
                }
            }
        }
        //depth of field

        int V = sizeOfTheFilterVertical;
        int H = sizeOfTheFilterHorizontal;
        float[] resColorImage_r = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        float[] resColorImage_g = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        float[] resColorImage_b = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        for (int i = 0; i < sizeOfTheFilterVertical; i++)
        {
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++)
            {
                float c = apertureDiameter * ((depthImage[i * sizeOfTheFilterVertical + j] - focusdistance) / depthImage[i * sizeOfTheFilterVertical + j]);
                //nem volt idom befejezni a merettol fuggo elmosodast
                float d = depthImage[i * sizeOfTheFilterVertical + j];
                if (d < dofNearMin)
                {//5x5 gauss
                    //r
                    if (i - 2 > 0 && j - 2 > 0 && (depthImage[(i - 2) * V + j - 2]>=dofNearMin || depthImage[(i - 2) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 2].r * 1f / 273f;
                    if (i - 2 > 0 && j - 1 > 0 && (depthImage[(i - 2) * V + j - 1]>=dofNearMin || depthImage[(i - 2) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 1].r * 4f / 273f;
                    if (i - 2 > 0 && j - 0 > 0 && (depthImage[(i - 2) * V + j - 0]>=dofNearMin || depthImage[(i - 2) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 0].r * 7f / 273f;
                    if (i - 2 > 0 && j + 1 < H && (depthImage[(i - 2) * V + j + 1]>=dofNearMin || depthImage[(i - 2) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 1].r * 4f / 273f;
                    if (i - 2 > 0 && j + 2 < H && (depthImage[(i - 2) * V + j + 2]>=dofNearMin || depthImage[(i - 2) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 2].r * 1f / 273f;
                    if (i - 1 > 0 && j - 2 > 0 && (depthImage[(i - 1) * V + j - 2]>=dofNearMin || depthImage[(i - 1) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 2].r * 4f / 273f;
                    if (i - 1 > 0 && j - 1 > 0 && (depthImage[(i - 1) * V + j - 1]>=dofNearMin || depthImage[(i - 1) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 1].r * 16f / 273f;
                    if (i - 1 > 0 && j - 0 > 0 && (depthImage[(i - 1) * V + j - 0]>=dofNearMin || depthImage[(i - 1) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 0].r * 26f / 273f;
                    if (i - 1 > 0 && j + 1 < H && (depthImage[(i - 1) * V + j + 1]>=dofNearMin || depthImage[(i - 1) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 1].r * 16f / 273f;
                    if (i - 1 > 0 && j + 2 < H && (depthImage[(i - 1) * V + j + 2]>=dofNearMin || depthImage[(i - 1) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 2].r * 4f / 273f;
                    if (i + 0 > 0 && j - 2 > 0 && (depthImage[(i + 0) * V + j - 2]>=dofNearMin || depthImage[(i + 0) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 2].r * 7f / 273f;
                    if (i + 0 > 0 && j - 1 > 0 && (depthImage[(i + 0) * V + j - 1]>=dofNearMin || depthImage[(i + 0) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 1].r * 26f / 273f;
                    if (i + 0 > 0 && j - 0 > 0 && (depthImage[(i + 0) * V + j - 0]>=dofNearMin || depthImage[(i + 0) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 0].r * 41f / 273f;
                    if (i + 0 > 0 && j + 1 < H && (depthImage[(i + 0) * V + j + 1]>=dofNearMin || depthImage[(i + 0) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 1].r * 26f / 273f;
                    if (i + 0 > 0 && j + 2 < H && (depthImage[(i + 0) * V + j + 2]>=dofNearMin || depthImage[(i + 0) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 2].r * 7f / 273f;
                    if (i + 1 < V && j - 2 > 0 && (depthImage[(i + 1) * V + j - 2]>=dofNearMin || depthImage[(i + 1) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 2].r * 4f / 273f;
                    if (i + 1 < V && j - 1 > 0 && (depthImage[(i + 1) * V + j - 1]>=dofNearMin || depthImage[(i + 1) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 1].r * 16f / 273f;
                    if (i + 1 < V && j - 0 > 0 && (depthImage[(i + 1) * V + j - 0]>=dofNearMin || depthImage[(i + 1) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 0].r * 26f / 273f;
                    if (i + 1 < V && j + 1 < H && (depthImage[(i + 1) * V + j + 1]>=dofNearMin || depthImage[(i + 1) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 1].r * 16f / 273f;
                    if (i + 1 < V && j + 2 < H && (depthImage[(i + 1) * V + j + 2]>=dofNearMin || depthImage[(i + 1) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 2].r * 4f / 273f;
                    if (i + 2 < V && j - 2 > 0 && (depthImage[(i + 2) * V + j - 2]>=dofNearMin || depthImage[(i + 2) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 2].r * 1f / 273f;
                    if (i + 2 < V && j - 1 > 0 && (depthImage[(i + 2) * V + j - 1]>=dofNearMin || depthImage[(i + 2) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 1].r * 4f / 273f;
                    if (i + 2 < V && j - 0 > 0 && (depthImage[(i + 2) * V + j - 0]>=dofNearMin || depthImage[(i + 2) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 0].r * 7f / 273f;
                    if (i + 2 < V && j + 1 < H && (depthImage[(i + 2) * V + j + 1]>=dofNearMin || depthImage[(i + 2) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 1].r * 4f / 273f;
                    if (i + 2 < V && j + 2 < H && (depthImage[(i + 2) * V + j + 2]>=dofNearMin || depthImage[(i + 2) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 2].r * 1f / 273f;
                    //g       
                    if (i - 2 > 0 && j - 2 > 0 && (depthImage[(i - 2) * V + j - 2]>=dofNearMin || depthImage[(i - 2) * V + j - 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 2].g * 1f / 273f;
                    if (i - 2 > 0 && j - 1 > 0 && (depthImage[(i - 2) * V + j - 1]>=dofNearMin || depthImage[(i - 2) * V + j - 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 1].g * 4f / 273f;
                    if (i - 2 > 0 && j - 0 > 0 && (depthImage[(i - 2) * V + j - 0]>=dofNearMin || depthImage[(i - 2) * V + j - 0] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 0].g * 7f / 273f;
                    if (i - 2 > 0 && j + 1 < H && (depthImage[(i - 2) * V + j + 1]>=dofNearMin || depthImage[(i - 2) * V + j + 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 1].g * 4f / 273f;
                    if (i - 2 > 0 && j + 2 < H && (depthImage[(i - 2) * V + j + 2]>=dofNearMin || depthImage[(i - 2) * V + j + 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 2].g * 1f / 273f;
                    if (i - 1 > 0 && j - 2 > 0 && (depthImage[(i - 1) * V + j - 2]>=dofNearMin || depthImage[(i - 1) * V + j - 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 2].g * 4f / 273f;
                    if (i - 1 > 0 && j - 1 > 0 && (depthImage[(i - 1) * V + j - 1]>=dofNearMin || depthImage[(i - 1) * V + j - 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 1].g * 16f / 273f;
                    if (i - 1 > 0 && j - 0 > 0 && (depthImage[(i - 1) * V + j - 0]>=dofNearMin || depthImage[(i - 1) * V + j - 0] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 0].g * 26f / 273f;
                    if (i - 1 > 0 && j + 1 < H && (depthImage[(i - 1) * V + j + 1]>=dofNearMin || depthImage[(i - 1) * V + j + 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 1].g * 16f / 273f;
                    if (i - 1 > 0 && j + 2 < H && (depthImage[(i - 1) * V + j + 2]>=dofNearMin || depthImage[(i - 1) * V + j + 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 2].g * 4f / 273f;
                    if (i + 0 > 0 && j - 2 > 0 && (depthImage[(i + 0) * V + j - 2]>=dofNearMin || depthImage[(i + 0) * V + j - 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 2].g * 7f / 273f;
                    if (i + 0 > 0 && j - 1 > 0 && (depthImage[(i + 0) * V + j - 1]>=dofNearMin || depthImage[(i + 0) * V + j - 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 1].g * 26f / 273f;
                    if (i + 0 > 0 && j - 0 > 0 && (depthImage[(i + 0) * V + j - 0]>=dofNearMin || depthImage[(i + 0) * V + j - 0] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 0].g * 41f / 273f;
                    if (i + 0 > 0 && j + 1 < H && (depthImage[(i + 0) * V + j + 1]>=dofNearMin || depthImage[(i + 0) * V + j + 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 1].g * 26f / 273f;
                    if (i + 0 > 0 && j + 2 < H && (depthImage[(i + 0) * V + j + 2]>=dofNearMin || depthImage[(i + 0) * V + j + 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 2].g * 7f / 273f;
                    if (i + 1 < V && j - 2 > 0 && (depthImage[(i + 1) * V + j - 2]>=dofNearMin || depthImage[(i + 1) * V + j - 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 2].g * 4f / 273f;
                    if (i + 1 < V && j - 1 > 0 && (depthImage[(i + 1) * V + j - 1]>=dofNearMin || depthImage[(i + 1) * V + j - 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 1].g * 16f / 273f;
                    if (i + 1 < V && j - 0 > 0 && (depthImage[(i + 1) * V + j - 0]>=dofNearMin || depthImage[(i + 1) * V + j - 0] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 0].g * 26f / 273f;
                    if (i + 1 < V && j + 1 < H && (depthImage[(i + 1) * V + j + 1]>=dofNearMin || depthImage[(i + 1) * V + j + 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 1].g * 16f / 273f;
                    if (i + 1 < V && j + 2 < H && (depthImage[(i + 1) * V + j + 2]>=dofNearMin || depthImage[(i + 1) * V + j + 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 2].g * 4f / 273f;
                    if (i + 2 < V && j - 2 > 0 && (depthImage[(i + 2) * V + j - 2]>=dofNearMin || depthImage[(i + 2) * V + j - 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 2].g * 1f / 273f;
                    if (i + 2 < V && j - 1 > 0 && (depthImage[(i + 2) * V + j - 1]>=dofNearMin || depthImage[(i + 2) * V + j - 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 1].g * 4f / 273f;
                    if (i + 2 < V && j - 0 > 0 && (depthImage[(i + 2) * V + j - 0]>=dofNearMin || depthImage[(i + 2) * V + j - 0] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 0].g * 7f / 273f;
                    if (i + 2 < V && j + 1 < H && (depthImage[(i + 2) * V + j + 1]>=dofNearMin || depthImage[(i + 2) * V + j + 1] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 1].g * 4f / 273f;
                    if (i + 2 < V && j + 2 < H && (depthImage[(i + 2) * V + j + 2]>=dofNearMin || depthImage[(i + 2) * V + j + 2] <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 2].g * 1f / 273f;
                    //b  
                    if (i - 2 > 0 && j - 2 > 0 && (depthImage[(i - 2) * V + j - 2]>=dofNearMin || depthImage[(i - 2) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 2].b * 1f / 273f;
                    if (i - 2 > 0 && j - 1 > 0 && (depthImage[(i - 2) * V + j - 1]>=dofNearMin || depthImage[(i - 2) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 1].b * 4f / 273f;
                    if (i - 2 > 0 && j - 0 > 0 && (depthImage[(i - 2) * V + j - 0]>=dofNearMin || depthImage[(i - 2) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 0].b * 7f / 273f;
                    if (i - 2 > 0 && j + 1 < H && (depthImage[(i - 2) * V + j + 1]>=dofNearMin || depthImage[(i - 2) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 1].b * 4f / 273f;
                    if (i - 2 > 0 && j + 2 < H && (depthImage[(i - 2) * V + j + 2]>=dofNearMin || depthImage[(i - 2) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 2].b * 1f / 273f;
                    if (i - 1 > 0 && j - 2 > 0 && (depthImage[(i - 1) * V + j - 2]>=dofNearMin || depthImage[(i - 1) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 2].b * 4f / 273f;
                    if (i - 1 > 0 && j - 1 > 0 && (depthImage[(i - 1) * V + j - 1]>=dofNearMin || depthImage[(i - 1) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 1].b * 16f / 273f;
                    if (i - 1 > 0 && j - 0 > 0 && (depthImage[(i - 1) * V + j - 0]>=dofNearMin || depthImage[(i - 1) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 0].b * 26f / 273f;
                    if (i - 1 > 0 && j + 1 < H && (depthImage[(i - 1) * V + j + 1]>=dofNearMin || depthImage[(i - 1) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 1].b * 16f / 273f;
                    if (i - 1 > 0 && j + 2 < H && (depthImage[(i - 1) * V + j + 2]>=dofNearMin || depthImage[(i - 1) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 2].b * 4f / 273f;
                    if (i + 0 > 0 && j - 2 > 0 && (depthImage[(i + 0) * V + j - 2]>=dofNearMin || depthImage[(i + 0) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 2].b * 7f / 273f;
                    if (i + 0 > 0 && j - 1 > 0 && (depthImage[(i + 0) * V + j - 1]>=dofNearMin || depthImage[(i + 0) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 1].b * 26f / 273f;
                    if (i + 0 > 0 && j - 0 > 0 && (depthImage[(i + 0) * V + j - 0]>=dofNearMin || depthImage[(i + 0) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 0].b * 41f / 273f;
                    if (i + 0 > 0 && j + 1 < H && (depthImage[(i + 0) * V + j + 1]>=dofNearMin || depthImage[(i + 0) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 1].b * 26f / 273f;
                    if (i + 0 > 0 && j + 2 < H && (depthImage[(i + 0) * V + j + 2]>=dofNearMin || depthImage[(i + 0) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 2].b * 7f / 273f;
                    if (i + 1 < V && j - 2 > 0 && (depthImage[(i + 1) * V + j - 2]>=dofNearMin || depthImage[(i + 1) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 2].b * 4f / 273f;
                    if (i + 1 < V && j - 1 > 0 && (depthImage[(i + 1) * V + j - 1]>=dofNearMin || depthImage[(i + 1) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 1].b * 16f / 273f;
                    if (i + 1 < V && j - 0 > 0 && (depthImage[(i + 1) * V + j - 0]>=dofNearMin || depthImage[(i + 1) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 0].b * 26f / 273f;
                    if (i + 1 < V && j + 1 < H && (depthImage[(i + 1) * V + j + 1]>=dofNearMin || depthImage[(i + 1) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 1].b * 16f / 273f;
                    if (i + 1 < V && j + 2 < H && (depthImage[(i + 1) * V + j + 2]>=dofNearMin || depthImage[(i + 1) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 2].b * 4f / 273f;
                    if (i + 2 < V && j - 2 > 0 && (depthImage[(i + 2) * V + j - 2]>=dofNearMin || depthImage[(i + 2) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 2].b * 1f / 273f;
                    if (i + 2 < V && j - 1 > 0 && (depthImage[(i + 2) * V + j - 1]>=dofNearMin || depthImage[(i + 2) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 1].b * 4f / 273f;
                    if (i + 2 < V && j - 0 > 0 && (depthImage[(i + 2) * V + j - 0]>=dofNearMin || depthImage[(i + 2) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 0].b * 7f / 273f;
                    if (i + 2 < V && j + 1 < H && (depthImage[(i + 2) * V + j + 1]>=dofNearMin || depthImage[(i + 2) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 1].b * 4f / 273f;
                    if (i + 2 < V && j + 2 < H && (depthImage[(i + 2) * V + j + 2]>=dofNearMin || depthImage[(i + 2) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 2].b * 1f / 273f;
                }                                              
                else if (d > dofFarMax)                        
                {                                              
                    //r                                        
                    if (i - 2 > 0 && j - 2 > 0 && (depthImage[(i - 2) * V + j - 2]>=dofNearMin || depthImage[(i - 2) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 2].r * 1f / 273f;
                    if (i - 2 > 0 && j - 1 > 0 && (depthImage[(i - 2) * V + j - 1]>=dofNearMin || depthImage[(i - 2) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 1].r * 4f / 273f;
                    if (i - 2 > 0 && j - 0 > 0 && (depthImage[(i - 2) * V + j - 0]>=dofNearMin || depthImage[(i - 2) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 0].r * 7f / 273f;
                    if (i - 2 > 0 && j + 1 < H && (depthImage[(i - 2) * V + j + 1]>=dofNearMin || depthImage[(i - 2) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 1].r * 4f / 273f;
                    if (i - 2 > 0 && j + 2 < H && (depthImage[(i - 2) * V + j + 2]>=dofNearMin || depthImage[(i - 2) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 2].r * 1f / 273f;
                    if (i - 1 > 0 && j - 2 > 0 && (depthImage[(i - 1) * V + j - 2]>=dofNearMin || depthImage[(i - 1) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 2].r * 4f / 273f;
                    if (i - 1 > 0 && j - 1 > 0 && (depthImage[(i - 1) * V + j - 1]>=dofNearMin || depthImage[(i - 1) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 1].r * 16f / 273f;
                    if (i - 1 > 0 && j - 0 > 0 && (depthImage[(i - 1) * V + j - 0]>=dofNearMin || depthImage[(i - 1) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 0].r * 26f / 273f;
                    if (i - 1 > 0 && j + 1 < H && (depthImage[(i - 1) * V + j + 1]>=dofNearMin || depthImage[(i - 1) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 1].r * 16f / 273f;
                    if (i - 1 > 0 && j + 2 < H && (depthImage[(i - 1) * V + j + 2]>=dofNearMin || depthImage[(i - 1) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 2].r * 4f / 273f;
                    if (i + 0 > 0 && j - 2 > 0 && (depthImage[(i + 0) * V + j - 2]>=dofNearMin || depthImage[(i + 0) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 2].r * 7f / 273f;
                    if (i + 0 > 0 && j - 1 > 0 && (depthImage[(i + 0) * V + j - 1]>=dofNearMin || depthImage[(i + 0) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 1].r * 26f / 273f;
                    if (i + 0 > 0 && j - 0 > 0 && (depthImage[(i + 0) * V + j - 0]>=dofNearMin || depthImage[(i + 0) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 0].r * 41f / 273f;
                    if (i + 0 > 0 && j + 1 < H && (depthImage[(i + 0) * V + j + 1]>=dofNearMin || depthImage[(i + 0) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 1].r * 26f / 273f;
                    if (i + 0 > 0 && j + 2 < H && (depthImage[(i + 0) * V + j + 2]>=dofNearMin || depthImage[(i + 0) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 2].r * 7f / 273f;
                    if (i + 1 < V && j - 2 > 0 && (depthImage[(i + 1) * V + j - 2]>=dofNearMin || depthImage[(i + 1) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 2].r * 4f / 273f;
                    if (i + 1 < V && j - 1 > 0 && (depthImage[(i + 1) * V + j - 1]>=dofNearMin || depthImage[(i + 1) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 1].r * 16f / 273f;
                    if (i + 1 < V && j - 0 > 0 && (depthImage[(i + 1) * V + j - 0]>=dofNearMin || depthImage[(i + 1) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 0].r * 26f / 273f;
                    if (i + 1 < V && j + 1 < H && (depthImage[(i + 1) * V + j + 1]>=dofNearMin || depthImage[(i + 1) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 1].r * 16f / 273f;
                    if (i + 1 < V && j + 2 < H && (depthImage[(i + 1) * V + j + 2]>=dofNearMin || depthImage[(i + 1) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 2].r * 4f / 273f;
                    if (i + 2 < V && j - 2 > 0 && (depthImage[(i + 2) * V + j - 2]>=dofNearMin || depthImage[(i + 2) * V + j - 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 2].r * 1f / 273f;
                    if (i + 2 < V && j - 1 > 0 && (depthImage[(i + 2) * V + j - 1]>=dofNearMin || depthImage[(i + 2) * V + j - 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 1].r * 4f / 273f;
                    if (i + 2 < V && j - 0 > 0 && (depthImage[(i + 2) * V + j - 0]>=dofNearMin || depthImage[(i + 2) * V + j - 0] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 0].r * 7f / 273f;
                    if (i + 2 < V && j + 1 < H && (depthImage[(i + 2) * V + j + 1]>=dofNearMin || depthImage[(i + 2) * V + j + 1] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 1].r * 4f / 273f;
                    if (i + 2 < V && j + 2 < H && (depthImage[(i + 2) * V + j + 2]>=dofNearMin || depthImage[(i + 2) * V + j + 2] <= dofFarMax)) resColorImage_r[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 2].r * 1f / 273f;
                    //g 
                    if (i - 2 > 0 && j - 2 > 0 && (depthImage[(i - 2) * V + j - 2]>=dofNearMin || depthImage[(i - 2) * V + j - 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 2].g * 1f / 273f;
                    if (i - 2 > 0 && j - 1 > 0 && (depthImage[(i - 2) * V + j - 1]>=dofNearMin || depthImage[(i - 2) * V + j - 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 1].g * 4f / 273f;
                    if (i - 2 > 0 && j - 0 > 0 && (depthImage[(i - 2) * V + j - 0]>=dofNearMin || depthImage[(i - 2) * V + j - 0]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 0].g * 7f / 273f;
                    if (i - 2 > 0 && j + 1 < H && (depthImage[(i - 2) * V + j + 1]>=dofNearMin || depthImage[(i - 2) * V + j + 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 1].g * 4f / 273f;
                    if (i - 2 > 0 && j + 2 < H && (depthImage[(i - 2) * V + j + 2]>=dofNearMin || depthImage[(i - 2) * V + j + 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 2].g * 1f / 273f;
                    if (i - 1 > 0 && j - 2 > 0 && (depthImage[(i - 1) * V + j - 2]>=dofNearMin || depthImage[(i - 1) * V + j - 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 2].g * 4f / 273f;
                    if (i - 1 > 0 && j - 1 > 0 && (depthImage[(i - 1) * V + j - 1]>=dofNearMin || depthImage[(i - 1) * V + j - 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 1].g * 16f / 273f;
                    if (i - 1 > 0 && j - 0 > 0 && (depthImage[(i - 1) * V + j - 0]>=dofNearMin || depthImage[(i - 1) * V + j - 0]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 0].g * 26f / 273f;
                    if (i - 1 > 0 && j + 1 < H && (depthImage[(i - 1) * V + j + 1]>=dofNearMin || depthImage[(i - 1) * V + j + 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 1].g * 16f / 273f;
                    if (i - 1 > 0 && j + 2 < H && (depthImage[(i - 1) * V + j + 2]>=dofNearMin || depthImage[(i - 1) * V + j + 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 2].g * 4f / 273f;
                    if (i + 0 > 0 && j - 2 > 0 && (depthImage[(i + 0) * V + j - 2]>=dofNearMin || depthImage[(i + 0) * V + j - 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 2].g * 7f / 273f;
                    if (i + 0 > 0 && j - 1 > 0 && (depthImage[(i + 0) * V + j - 1]>=dofNearMin || depthImage[(i + 0) * V + j - 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 1].g * 26f / 273f;
                    if (i + 0 > 0 && j - 0 > 0 && (depthImage[(i + 0) * V + j - 0]>=dofNearMin || depthImage[(i + 0) * V + j - 0]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 0].g * 41f / 273f;
                    if (i + 0 > 0 && j + 1 < H && (depthImage[(i + 0) * V + j + 1]>=dofNearMin || depthImage[(i + 0) * V + j + 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 1].g * 26f / 273f;
                    if (i + 0 > 0 && j + 2 < H && (depthImage[(i + 0) * V + j + 2]>=dofNearMin || depthImage[(i + 0) * V + j + 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 2].g * 7f / 273f;
                    if (i + 1 < V && j - 2 > 0 && (depthImage[(i + 1) * V + j - 2]>=dofNearMin || depthImage[(i + 1) * V + j - 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 2].g * 4f / 273f;
                    if (i + 1 < V && j - 1 > 0 && (depthImage[(i + 1) * V + j - 1]>=dofNearMin || depthImage[(i + 1) * V + j - 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 1].g * 16f / 273f;
                    if (i + 1 < V && j - 0 > 0 && (depthImage[(i + 1) * V + j - 0]>=dofNearMin || depthImage[(i + 1) * V + j - 0]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 0].g * 26f / 273f;
                    if (i + 1 < V && j + 1 < H && (depthImage[(i + 1) * V + j + 1]>=dofNearMin || depthImage[(i + 1) * V + j + 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 1].g * 16f / 273f;
                    if (i + 1 < V && j + 2 < H && (depthImage[(i + 1) * V + j + 2]>=dofNearMin || depthImage[(i + 1) * V + j + 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 2].g * 4f / 273f;
                    if (i + 2 < V && j - 2 > 0 && (depthImage[(i + 2) * V + j - 2]>=dofNearMin || depthImage[(i + 2) * V + j - 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 2].g * 1f / 273f;
                    if (i + 2 < V && j - 1 > 0 && (depthImage[(i + 2) * V + j - 1]>=dofNearMin || depthImage[(i + 2) * V + j - 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 1].g * 4f / 273f;
                    if (i + 2 < V && j - 0 > 0 && (depthImage[(i + 2) * V + j - 0]>=dofNearMin || depthImage[(i + 2) * V + j - 0]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 0].g * 7f / 273f;
                    if (i + 2 < V && j + 1 < H && (depthImage[(i + 2) * V + j + 1]>=dofNearMin || depthImage[(i + 2) * V + j + 1]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 1].g * 4f / 273f;
                    if (i + 2 < V && j + 2 < H && (depthImage[(i + 2) * V + j + 2]>=dofNearMin || depthImage[(i + 2) * V + j + 2]  <= dofFarMax)) resColorImage_g[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 2].g * 1f / 273f;
                    //b                                       (     )                                        (     )
                    if (i - 2 > 0 && j - 2 > 0 && (depthImage[(i - 2) * V + j - 2]>=dofNearMin || depthImage[(i - 2) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 2].b * 1f / 273f;
                    if (i - 2 > 0 && j - 1 > 0 && (depthImage[(i - 2) * V + j - 1]>=dofNearMin || depthImage[(i - 2) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j - 1].b * 4f / 273f;
                    if (i - 2 > 0 && j - 0 > 0 && (depthImage[(i - 2) * V + j - 0]>=dofNearMin || depthImage[(i - 2) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 0].b * 7f / 273f;
                    if (i - 2 > 0 && j + 1 < H && (depthImage[(i - 2) * V + j + 1]>=dofNearMin || depthImage[(i - 2) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 1].b * 4f / 273f;
                    if (i - 2 > 0 && j + 2 < H && (depthImage[(i - 2) * V + j + 2]>=dofNearMin || depthImage[(i - 2) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 2) * sizeOfTheFilterVertical + j + 2].b * 1f / 273f;
                    if (i - 1 > 0 && j - 2 > 0 && (depthImage[(i - 1) * V + j - 2]>=dofNearMin || depthImage[(i - 1) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 2].b * 4f / 273f;
                    if (i - 1 > 0 && j - 1 > 0 && (depthImage[(i - 1) * V + j - 1]>=dofNearMin || depthImage[(i - 1) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j - 1].b * 16f / 273f;
                    if (i - 1 > 0 && j - 0 > 0 && (depthImage[(i - 1) * V + j - 0]>=dofNearMin || depthImage[(i - 1) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 0].b * 26f / 273f;
                    if (i - 1 > 0 && j + 1 < H && (depthImage[(i - 1) * V + j + 1]>=dofNearMin || depthImage[(i - 1) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 1].b * 16f / 273f;
                    if (i - 1 > 0 && j + 2 < H && (depthImage[(i - 1) * V + j + 2]>=dofNearMin || depthImage[(i - 1) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i - 1) * sizeOfTheFilterVertical + j + 2].b * 4f / 273f;
                    if (i + 0 > 0 && j - 2 > 0 && (depthImage[(i + 0) * V + j - 2]>=dofNearMin || depthImage[(i + 0) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 2].b * 7f / 273f;
                    if (i + 0 > 0 && j - 1 > 0 && (depthImage[(i + 0) * V + j - 1]>=dofNearMin || depthImage[(i + 0) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j - 1].b * 26f / 273f;
                    if (i + 0 > 0 && j - 0 > 0 && (depthImage[(i + 0) * V + j - 0]>=dofNearMin || depthImage[(i + 0) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 0].b * 41f / 273f;
                    if (i + 0 > 0 && j + 1 < H && (depthImage[(i + 0) * V + j + 1]>=dofNearMin || depthImage[(i + 0) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 1].b * 26f / 273f;
                    if (i + 0 > 0 && j + 2 < H && (depthImage[(i + 0) * V + j + 2]>=dofNearMin || depthImage[(i + 0) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 0) * sizeOfTheFilterVertical + j + 2].b * 7f / 273f;
                    if (i + 1 < V && j - 2 > 0 && (depthImage[(i + 1) * V + j - 2]>=dofNearMin || depthImage[(i + 1) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 2].b * 4f / 273f;
                    if (i + 1 < V && j - 1 > 0 && (depthImage[(i + 1) * V + j - 1]>=dofNearMin || depthImage[(i + 1) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j - 1].b * 16f / 273f;
                    if (i + 1 < V && j - 0 > 0 && (depthImage[(i + 1) * V + j - 0]>=dofNearMin || depthImage[(i + 1) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 0].b * 26f / 273f;
                    if (i + 1 < V && j + 1 < H && (depthImage[(i + 1) * V + j + 1]>=dofNearMin || depthImage[(i + 1) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 1].b * 16f / 273f;
                    if (i + 1 < V && j + 2 < H && (depthImage[(i + 1) * V + j + 2]>=dofNearMin || depthImage[(i + 1) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 1) * sizeOfTheFilterVertical + j + 2].b * 4f / 273f;
                    if (i + 2 < V && j - 2 > 0 && (depthImage[(i + 2) * V + j - 2]>=dofNearMin || depthImage[(i + 2) * V + j - 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 2].b * 1f / 273f;
                    if (i + 2 < V && j - 1 > 0 && (depthImage[(i + 2) * V + j - 1]>=dofNearMin || depthImage[(i + 2) * V + j - 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j - 1].b * 4f / 273f;
                    if (i + 2 < V && j - 0 > 0 && (depthImage[(i + 2) * V + j - 0]>=dofNearMin || depthImage[(i + 2) * V + j - 0] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 0].b * 7f / 273f;
                    if (i + 2 < V && j + 1 < H && (depthImage[(i + 2) * V + j + 1]>=dofNearMin || depthImage[(i + 2) * V + j + 1] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 1].b * 4f / 273f;
                    if (i + 2 < V && j + 2 < H && (depthImage[(i + 2) * V + j + 2]>=dofNearMin || depthImage[(i + 2) * V + j + 2] <= dofFarMax)) resColorImage_b[i*V+j] += rawColorImage[(i + 2) * sizeOfTheFilterVertical + j + 2].b * 1f / 273f;
                }
                else
                {
                    resColorImage_r[i*V+j] = rawColorImage[i * sizeOfTheFilterVertical + j].r;
                    resColorImage_g[i*V+j] = rawColorImage[i * sizeOfTheFilterVertical + j].g;
                    resColorImage_b[i * V + j] = rawColorImage[i * sizeOfTheFilterVertical + j].b;
                }
            }
        }
        for (int i = 0; i < sizeOfTheFilterVertical; i++)
        {
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++)
            {
                rawColorImage[i * sizeOfTheFilterVertical + j].r = resColorImage_r[i*V+j];
                rawColorImage[i * sizeOfTheFilterVertical + j].g = resColorImage_g[i*V+j];
                rawColorImage[i * sizeOfTheFilterVertical + j].b = resColorImage_b[i * V + j];
            } 
        }

        Debug.Log(rawColorImage[0]);
    if (rawImageOutput != null)
        {
            Texture2D texture = new Texture2D(sizeOfTheFilterVertical, sizeOfTheFilterHorizontal);
            texture.SetPixels(rawColorImage);
            texture.Apply();
            rawImageOutput.texture = texture;
        }
    }
    //functions
    void CastRay()
    {
        QueryParameters queryParameters = QueryParameters.Default;
        queryParameters.layerMask = LayerMask.GetMask("Default");
        _jobHandle.Complete();
        //következö raycastok elinditása
        Ray ray = cam.ScreenPointToRay(new Vector3(Mathf.Round(cam.pixelWidth / 2), Mathf.Round(cam.pixelHeight / 2), 0));
        Ray rayOrigine = cam.ScreenPointToRay(Input.mousePosition);
        for (int i = 0; i < sizeOfTheFilterVertical; ++i) {
            float yOffset = ((sizeOfTheFilterHorizontal - 1) / 2f) - i;
            for (int j = 0; j < sizeOfTheFilterHorizontal; ++j) {
                float xOffset = j - ((sizeOfTheFilterVertical - 1) / 2f);
                Vector3 position = ray.origin;
                Quaternion angleRight = Quaternion.AngleAxis((angleOfViewHorizontal / sizeOfTheFilterHorizontal) * xOffset, transform.up);
                Quaternion angleDown = Quaternion.AngleAxis((angleOfViewVertical / sizeOfTheFilterVertical) * yOffset, transform.right);

                //Debug.Log((angleOfViewVertical / sizeOfTheFilterVertical) * yOffset);
                Vector3 direction = angleRight * (angleDown * ray.direction);
                //bool isHit = Physics.Raycast(position, direction, out hit, viewMaxDistance);
                //QueryParameters queryParameters = QueryParameters();
                
                _raycastCommands[(i * sizeOfTheFilterVertical) + j] = new RaycastCommand(position, direction, queryParameters, viewMaxDistance);
                //régi verzió
                //_raycastCommands[i * sizeOfTheFilterVertical + j] = new RaycastCommand(position, direction, viewMaxDistance, 0/* ~0u layermasj*/, 1/*max hit*/);

            }
        }
        //JobHandle handle = prepareCommandsJob.ScheduleParallel(commands.Length, 64, default);
        int commandsPerJob = (int)Mathf.Floor(Mathf.Max((sizeOfTheFilterVertical * sizeOfTheFilterHorizontal) / JobsUtility.JobWorkerCount, 1));
        //handle = RaycastCommand.ScheduleBatch(commands, results, commandsPerJob, maxHits, );
        Debug.Log("egységek: " + commandsPerJob);
        _jobHandle = RaycastCommand.ScheduleBatch(_raycastCommands, _raycastHits, commandsPerJob, 1, default(JobHandle));


        
    }

    void eredetiCastRay()
    {
        //todo: rawColorImage hova kellene elhelyezni
        Color[] rawColorImage = new Color[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        float[] depthImage = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
         
        RaycastHit hit;
        Ray ray = cam.ScreenPointToRay(new Vector3(Mathf.Round(cam.pixelWidth / 2), Mathf.Round(cam.pixelHeight / 2), 0));
        

        Ray rayOrigine = cam.ScreenPointToRay(Input.mousePosition);
        string sum = "";
        for (int i =0; i < sizeOfTheFilterVertical;i++) {
            float yOffset = ((sizeOfTheFilterHorizontal-1) / 2f)-i;
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++){
                //------------------
                //raycast
                float xOffset = j-((sizeOfTheFilterVertical-1) / 2f);
                sum +="[ "+ xOffset + " : " + yOffset + " ]";
                Vector3 position = ray.origin;
                //szök oldalra és lefele

                Quaternion angleRight = Quaternion.AngleAxis( (angleOfViewHorizontal / sizeOfTheFilterHorizontal) * xOffset, transform.up);

                Quaternion angleDown = Quaternion.AngleAxis( (angleOfViewVertical / sizeOfTheFilterVertical) * yOffset, transform.right);
                Debug.Log((angleOfViewVertical / sizeOfTheFilterVertical) * yOffset);
                Vector3 direction = angleRight * (angleDown * ray.direction);
                bool isHit = Physics.Raycast(position, direction, out hit, viewMaxDistance);


                //-----------------------
                //get color
                if (isHit){
                    //Debug.DrawLine(transform.position, hit.point, Color.cyan, 2.5f);//debug line

                    Renderer rend = hit.transform.GetComponent<Renderer>();
                    MeshCollider meshCollider = hit.collider as MeshCollider;
                    if (rend == null || rend.sharedMaterial == null || rend.sharedMaterial.mainTexture == null || meshCollider == null)
                    {
                        string problem = "probléma: ";
                        if (rend == null) problem += "nincs renderer\n";
                        if (rend.sharedMaterial == null) problem += "nincs shader material\n";
                        if (rend.sharedMaterial.mainTexture == null) problem += "nincs mainTextura\n";
                        if (meshCollider == null) problem += "nincs meshCollider\n";
                        Debug.Log(problem);
                        return;
                    }

                    Texture2D tex = rend.material.mainTexture as Texture2D;
                    Vector2 pixelUV = hit.textureCoord;

                    pixelUV.x *= tex.width;
                    pixelUV.y *= tex.height;
                    //Debug.Log("pixelUV.x:" + (pixelUV.x).ToString());
                    //Debug.Log("pixelUV.y:" + (pixelUV.y).ToString());
                    rawColorImage[i * sizeOfTheFilterVertical + j] = tex.GetPixel((int)pixelUV.x, (int)pixelUV.y);
                    depthImage[i * sizeOfTheFilterVertical + j] = hit.distance;
                }
                else{
                    //Debug.Log("Did not Hit");
                    rawColorImage[i * sizeOfTheFilterVertical + j] = Color.blue;
                    depthImage[i * sizeOfTheFilterVertical + j] = -1;
                }
            }
            if (rawImageOutput != null)
            {
                Texture2D texture = new Texture2D(sizeOfTheFilterVertical, sizeOfTheFilterHorizontal);
                texture.SetPixels(rawColorImage);
                texture.Apply();
                rawImageOutput.texture = texture;
            }
        }
    }
    //main functions
    void Start()
    {
        characterController = GetComponent<CharacterController>();
        cam = this.gameObject.transform.GetChild(0).GetComponent<Camera>();

        // Lock cursor
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    void Update()
    {
        // We are grounded, so recalculate move direction based on axes
        Vector3 forward = transform.TransformDirection(Vector3.forward);
        Vector3 right = transform.TransformDirection(Vector3.right);
        // Press Left Shift to run
        bool isRunning = Input.GetKey(KeyCode.LeftShift);
        float curSpeedX = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Vertical") : 0;
        float curSpeedY = canMove ? (isRunning ? runningSpeed : walkingSpeed) * Input.GetAxis("Horizontal") : 0;
        float movementDirectionY = moveDirection.y;
        moveDirection = (forward * curSpeedX) + (right * curSpeedY);

        if (Input.GetButton("Jump") && canMove && characterController.isGrounded)
        {
            moveDirection.y = jumpSpeed;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        // Apply gravity. Gravity is multiplied by deltaTime twice (once here, and once below
        // when the moveDirection is multiplied by deltaTime). This is because gravity should be applied
        // as an acceleration (ms^-2)
        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.deltaTime;
        }

        // Move the controller
        characterController.Move(moveDirection * Time.deltaTime);

        // Player and Camera rotation
        if (canMove)
        {
            rotationX += -Input.GetAxis("Mouse Y") * lookSpeed;
            rotationX = Mathf.Clamp(rotationX, -lookXLimit, lookXLimit);
            cam.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
            transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeed, 0);
        }
        //raycast
        if (Input.GetMouseButtonDown(2))
        {
            //eredetiCastRay();
            CastRay();
            isNextRaycast = true;
        }
        if (isNextRaycast && _jobHandle.IsCompleted) {
            CompliteRayCast();
        }
    }


}
