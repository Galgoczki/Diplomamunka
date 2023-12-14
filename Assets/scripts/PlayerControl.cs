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

    public struct RayResult
    {

    }

    //[BurstCompile]
    private struct PrepareRaycastCommandsJob : IJobFor
    {
        [ReadOnly]
        public NativeArray<Vector3> positions;

        [ReadOnly]
        public NativeArray<Vector3> directions;

        [NativeDisableParallelForRestriction]
        public NativeArray<RaycastCommand> commands;

        public int layerMask;

        public void Execute(int index)
        {
            Vector3 position = this.positions[index];
            Vector3 direction = this.directions[index];
            this.commands[index] = new RaycastCommand(
                position, direction, new QueryParameters(this.layerMask), 50);
        }
    }
    void Awake()
    {
        int size = sizeOfTheFilterHorizontal * sizeOfTheFilterVertical;
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
                    depthImage[i * sizeOfTheFilterVertical + j] = _raycastHits[i * sizeOfTheFilterVertical + j].distance;
                }
                else
                {
                    rawColorImage[i * sizeOfTheFilterVertical + j] = Color.blue;
                    depthImage[i * sizeOfTheFilterVertical + j] = -1;
                }
            }
        }
        //depth of field
        float H = flenght + (flenght * flenght) / (fnumber*coc);
        float dofFarMax=(H*focusdistance)/(H - (focusdistance-flenght));
        float dofNearMin=(H*focusdistance)/(H + (focusdistance- flenght));
        float dof= dofFarMax- dofNearMin;
        //float dof= (2*focusdistance*focusdistance*fnumber*coc)/(flenght*flenght);
        Debug.Log(dofFarMax);
        Debug.Log(dofNearMin);
        Debug.Log(dof);

        /*for (int i = 0; i < sizeOfTheFilterVertical; i++)
        {
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++)
            {

            }
        }*/



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
        string sum = "";
        for (int i = 0; i < sizeOfTheFilterVertical; i++) {
            float yOffset = ((sizeOfTheFilterHorizontal - 1) / 2f) - i;
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++) {
                float xOffset = j - ((sizeOfTheFilterVertical - 1) / 2f);
                sum += "[ " + xOffset + " : " + yOffset + " ]";
                Vector3 position = ray.origin;
                Quaternion angleRight = Quaternion.AngleAxis((angleOfViewHorizontal / sizeOfTheFilterHorizontal) * xOffset, transform.up);
                Quaternion angleDown = Quaternion.AngleAxis((angleOfViewVertical / sizeOfTheFilterVertical) * yOffset, transform.right);

                //Debug.Log((angleOfViewVertical / sizeOfTheFilterVertical) * yOffset);
                Vector3 direction = angleRight * (angleDown * ray.direction);
                //bool isHit = Physics.Raycast(position, direction, out hit, viewMaxDistance);
                //QueryParameters queryParameters = QueryParameters();
                _raycastCommands[i * sizeOfTheFilterVertical + j] = new RaycastCommand(position, direction, queryParameters, viewMaxDistance);
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
