using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Unity.Jobs;
using Unity.Collections;
using Unity.Jobs.LowLevel.Unsafe;
using TMPro;

public class gauss2d {
    private int x;
    private int y;
    private float[,] gauss;

    public gauss2d(int x, int y){
        float sigma = 2.0f;
        float sum = 0f;
        this.x = x;
        this.y = y;
        this.gauss = new float[x , y];
        for (int i = 0; i < this.x; ++i) {
            int ii = i- Convert.ToInt32(Math.Floor((float)this.x / 2));
            for (int j = 0; j < this.y; ++j) {
                int jj = j - Convert.ToInt32(Math.Floor((float)this.y / 2));
                this.gauss[i , j] = (float)(Math.Exp(-(ii * ii + jj * jj) / (2 * sigma * sigma)) / (2 * Math.PI * sigma * sigma));
                sum += this.gauss[i , j];
            }
        }
        //normalise
        for (int i = 0; i < this.x; i++)
        {
            for (int j = 0; j < this.y; j++)
            {
                this.gauss[i ,j] = this.gauss[i , j] / sum;
            }
        }
    }
    public float getX(){ return this.x; }
    public float getY(){ return this.y; }
    public float getGauss(int x, int y) {
        if (x>0 && x<this.x && y>0 && y<this.y)
        {
            return this.gauss[x , y];
        }
        return 0;
    }
    public string printGauss() {
        string tmp = "";
        for (int i = 0; i < this.x; i++)
        {
            for (int j = 0; j < this.y; j++)
            {
                tmp += gauss[i , j].ToString()+" ";
            }
            tmp += "\n";
        }
        return tmp;
    }
}
[RequireComponent(typeof(CharacterController))]
public class PlayerControl : MonoBehaviour
{
    //horizontal=x=width=i
    //vertikal=y=height=j
    private Camera cam;
    public TMP_InputField PoWInputWidth;
    public TMP_InputField PoWInputHeight;
    public TMP_InputField SensorInputWidth;
    public TMP_InputField SensorInputHeight;
    public TMP_InputField SensorSizeMmWidth;
    public TMP_InputField SensorSizeMmHeight;
    public TMP_InputField flenghtInput;
    public TextMeshProUGUI dataoutput;
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
    public float sensorSizeVertikal = 35f;
    public RawImage rawImageOutput = null;
    public GameObject imageOutput = null;

    CharacterController characterController;
    Vector3 moveDirection = Vector3.zero;
    float rotationX = 0;

    [HideInInspector]
    public bool canMove = true;

    //todo: privet serializ
    public float viewMaxDistance = 50f;
    public float angleOfViewHorizontal = 16f;
    public float angleOfViewVertical = 24f;
    public int sizeOfTheFilterHorizontal = 10;
    public int sizeOfTheFilterVertical = 10;

    //
    private NativeArray<RaycastCommand> _raycastCommands;
    private NativeArray<RaycastHit> _raycastHits;
    private JobHandle _jobHandle;
    private bool isNextRaycast = false;

    //ellenörizni hogy frissiteni kell e a raycasthoz a táblázatot
    public int pre_sizeOfTheFilterHorizontal;
    public int pre_sizeOfTheFilterVertical;

    void Awake()
    {
        pre_sizeOfTheFilterHorizontal = sizeOfTheFilterHorizontal;
        pre_sizeOfTheFilterVertical = sizeOfTheFilterVertical;
        int size = sizeOfTheFilterVertical * sizeOfTheFilterHorizontal;
        _raycastCommands = new NativeArray<RaycastCommand>(size, Allocator.Persistent);
        _raycastHits = new NativeArray<RaycastHit>(size, Allocator.Persistent);



        //inicializálás
        float h = flenght + (flenght * flenght) / (fnumber * coc);
        float dofFarMax = (h * focusdistance) / (h - (focusdistance - flenght));
        float dofNearMin = (h * focusdistance) / (h + (focusdistance - flenght));
        float dof = dofFarMax - dofNearMin;
        float f2 = flenght * flenght;
        float pixelpermmx = sizeOfTheFilterHorizontal / sensorSizeHorizontal;
        float pixelpermmy = sizeOfTheFilterVertical / sensorSizeVertikal;
        Color[] rawColorImage = new Color[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        float[] depthImage = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        //eredménykiirás
        dataoutput.text = "adatok\nlátószög: " + angleOfViewHorizontal.ToString() + ":" + angleOfViewVertical.ToString() +
            "\nszenzor mérete: " + sensorSizeHorizontal.ToString() + ":" + sensorSizeVertikal + " mm\nFelbontás: " + sizeOfTheFilterHorizontal.ToString() + ":" + sizeOfTheFilterVertical.ToString() +
            "\nfokusz távolság:" + flenght.ToString() + "mm\nmélységéleség maximum távolsság: " + dofFarMax.ToString() + "mm\nmélységéleség minimum távolsság: " + dofNearMin.ToString() + "mm\nmélységéleség: " + dof.ToString()+"mm";
    }

    private void OnDestroy()
    {
        _jobHandle.Complete();
        _raycastCommands.Dispose();
        _raycastHits.Dispose();
    }
    private void resetCamera() {
        Debug.Log("reset-> "+ sizeOfTheFilterVertical.ToString() + " : "+ sizeOfTheFilterHorizontal.ToString());
        _jobHandle.Complete();
        _raycastCommands.Dispose();
        _raycastHits.Dispose();
        pre_sizeOfTheFilterHorizontal = sizeOfTheFilterHorizontal;
        pre_sizeOfTheFilterVertical = sizeOfTheFilterVertical;
        int size = sizeOfTheFilterVertical * sizeOfTheFilterHorizontal;
        _raycastCommands = new NativeArray<RaycastCommand>(size, Allocator.Persistent);
        _raycastHits = new NativeArray<RaycastHit>(size, Allocator.Persistent);
    }
    public void PoWInputWidthOnChange() { angleOfViewHorizontal = Convert.ToInt32(PoWInputWidth.text); Debug.Log(PoWInputWidth.text); }
    public void PoWInputHeightOnChange() { angleOfViewVertical = Convert.ToInt32(PoWInputHeight.text); Debug.Log(PoWInputHeight.text); }
    public void SensorInputWidthOnChange() { sizeOfTheFilterHorizontal = Convert.ToInt32(SensorInputWidth.text); Debug.Log(SensorInputWidth.text); }
    public void SensorInputHeightOnChange() { sizeOfTheFilterVertical = Convert.ToInt32(SensorInputHeight.text); Debug.Log(SensorInputHeight.text); }
    public void SensorSizeMmWidthOnChange() { sensorSizeHorizontal = Convert.ToInt32(SensorSizeMmWidth.text); Debug.Log(SensorSizeMmWidth.text); }
    public void SensorSizeMmHeightOnChange() { sensorSizeVertikal = Convert.ToInt32(SensorSizeMmHeight.text); Debug.Log(SensorSizeMmHeight.text); }
    public void FlenghtOnChange() { flenght = (float)Convert.ToDouble(flenghtInput.text); Debug.Log(flenghtInput.text); }

    private void CompliteRayCast()
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
        float pixelpermmy = sizeOfTheFilterVertical / sensorSizeVertikal;
        Debug.Log("pixelpermmx: " + pixelpermmx.ToString());
        Debug.Log("pixelpermmy: " + pixelpermmy.ToString());
        //adat feldolgozása
        Color[] rawColorImage = new Color[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];
        float[] depthImage = new float[sizeOfTheFilterHorizontal * sizeOfTheFilterVertical];

        //eredménykiirás
        dataoutput.text = "adatok\nlátószög: "+ angleOfViewHorizontal.ToString() + ":"+ angleOfViewVertical.ToString() + 
            "\nszenzor mérete: " + sensorSizeHorizontal.ToString() + ":"+ sensorSizeVertikal + " mm\nFelbontás: "+ sizeOfTheFilterHorizontal.ToString() + ":"+ sizeOfTheFilterVertical.ToString() + 
            "\nfokusz távolság:"+flenght.ToString()+"mm\nmélységéleség maximum távolsság: " + dofFarMax.ToString() + "mm\nmélységéleség minimum távolsság: " + dofNearMin.ToString() + "mm\nmélységéleség: " + dof.ToString()+"mm";

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
        Dictionary<Vector2, gauss2d > hashmap = new Dictionary<Vector2, gauss2d > ();
        for (int i = 0; i < sizeOfTheFilterVertical; i++)
        {
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++)
            {
                float c = apertureDiameter * ((depthImage[i * sizeOfTheFilterVertical + j] - focusdistance) / depthImage[i * sizeOfTheFilterVertical + j]);//mm és fel kellene szorozni a szenzorok száma/mmrel
                int cPerSensorx = (int)Math.Floor(c * pixelpermmx);
                int cPerSensory = (int)Math.Floor(c * pixelpermmy);
                cPerSensorx = cPerSensorx % 2 == 0 ? cPerSensorx + 1 : cPerSensorx;
                cPerSensory = cPerSensory % 2 == 0 ? cPerSensory + 1 : cPerSensory;
                //Debug.Log(cPerSensorx);
                //Debug.Log(cPerSensory);
                float d = depthImage[i * sizeOfTheFilterVertical + j];
                if (cPerSensorx <= 1 || cPerSensory <= 1) {
                    continue;
                }
                if (!hashmap.ContainsKey(new Vector2(cPerSensorx, cPerSensory)))
                {
                    hashmap.Add(new Vector2(cPerSensorx, cPerSensory), new gauss2d(cPerSensorx, cPerSensory));
                }
                gauss2d currentGauss = null;
                hashmap.TryGetValue(new Vector2(cPerSensorx, cPerSensory), out currentGauss);
                float resr = 0;
                float resg = 0;
                float resb = 0;
                float reference = 0;
                for (int ii = -(int)((currentGauss.getX() - 1) / 2); ii <= (int)((currentGauss.getX()-1)/2); ii++) {
                    for (int jj = -(int)((currentGauss.getY() - 1) / 2); jj <= (int)((currentGauss.getY()-1)/2); jj++) {
                        float multi = currentGauss.getGauss(ii + (int)((currentGauss.getX() - 1) / 2), jj + (int)((currentGauss.getX() - 1) / 2));
                        if (i + ii > 0 && i + ii < sizeOfTheFilterVertical && j + jj > 0 && j + jj < sizeOfTheFilterHorizontal) {
                            resr += rawColorImage[(i + ii) * sizeOfTheFilterVertical + (j + jj)].r * multi;
                            resg += rawColorImage[(i + ii) * sizeOfTheFilterVertical + (j + jj)].g * multi;
                            resb += rawColorImage[(i + ii) * sizeOfTheFilterVertical + (j + jj)].b * multi;
                            reference += 1 * multi;
                        } else {
                            resr += rawColorImage[i * sizeOfTheFilterVertical + j].r * multi;
                            resg += rawColorImage[i * sizeOfTheFilterVertical + j].g * multi;
                            resb += rawColorImage[i * sizeOfTheFilterVertical + j].b * multi;
                            reference += 1 * multi;
                        }
                    } 
                }
                float referencMultiple = (reference < 0.99 || reference > 1.01) ? reference : 1;
                if(referencMultiple==0)referencMultiple = 1;
                rawColorImage[i * sizeOfTheFilterVertical + j].r = resr/ referencMultiple;
                rawColorImage[i * sizeOfTheFilterVertical + j].g = resg/ referencMultiple;
                rawColorImage[i * sizeOfTheFilterVertical + j].b = resb/ referencMultiple;
            }
        }
    if (rawImageOutput != null)
        {
            Texture2D texture = new Texture2D(sizeOfTheFilterHorizontal, sizeOfTheFilterVertical);
            texture.SetPixels(rawColorImage);
            texture.Apply();
            RectTransform rt = imageOutput.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2( sizeOfTheFilterHorizontal, sizeOfTheFilterVertical);
            imageOutput.transform.position = new Vector3(sizeOfTheFilterHorizontal, sizeOfTheFilterVertical, 0);
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
        for (int i = 0; i < sizeOfTheFilterVertical; i++) {
            float yOffset = ((sizeOfTheFilterHorizontal - 1) / 2f) - i;
            for (int j = 0; j < sizeOfTheFilterHorizontal; j++) {
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
            if (pre_sizeOfTheFilterHorizontal != sizeOfTheFilterHorizontal || pre_sizeOfTheFilterVertical != sizeOfTheFilterVertical) {
                resetCamera();
            }
            //eredetiCastRay();
            CastRay();
            isNextRaycast = true;
        }
        if (isNextRaycast && _jobHandle.IsCompleted) {
            CompliteRayCast();
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (Cursor.lockState == CursorLockMode.Locked){
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
            else {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        //kilépés
        if (Input.GetKey("escape"))
        {
            Application.Quit();
        }
    }


}
