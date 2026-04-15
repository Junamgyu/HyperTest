using UnityEngine;

public class CustomGravityBody : MonoBehaviour
{
    [SerializeField] private float _gravityMultiplier = 2.5f;
    [SerializeField] private float _fallMultiplier = 4.0f;

    private Rigidbody _rb;
    private Vector3 _gravityDir = Vector3.down;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _rb.useGravity = false;
        _rb.freezeRotation = true;
    }
    private void Start()
    {
        if(GravitySystem.Instance != null)
        {
            _gravityDir = GravitySystem.Instance.GravityDirection;
            GravitySystem.Instance.OnGravityDirectionChanged += OnGravityChanged;
        }
    }

    private void OnDisable()
    {
        if(GravitySystem.Instance != null)
            GravitySystem.Instance.OnGravityDirectionChanged -= OnGravityChanged;
    }

    private void FixedUpdate()
    {
        float gravMag = GravitySystem.Instance != null ? GravitySystem.Instance.GravityMagnitude : 9.81f;
        
        bool isFalling = Vector3.Dot(_rb.linearVelocity, _gravityDir) > 0f;
        float multiplier = isFalling ? _fallMultiplier : _gravityMultiplier;

        _rb.AddForce(_gravityDir * (gravMag * multiplier), ForceMode.Acceleration);
    }

    private void OnGravityChanged(Vector3 newDir)
    {
        _gravityDir = newDir;

        Vector3 horizVel = Vector3.ProjectOnPlane(_rb.linearVelocity, _gravityDir);
        _rb.linearVelocity = horizVel;
    }


    // Update is called once per frame
    void Update()
    {
        
    }
}
