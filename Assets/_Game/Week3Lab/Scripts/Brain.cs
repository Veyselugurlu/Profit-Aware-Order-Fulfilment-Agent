using UnityEngine;

[RequireComponent(typeof(Rigidbody), typeof(CapsuleCollider))]
public sealed class Brain : MonoBehaviour
{
    [Header("Ground sensor")]
    public Transform eyes;
    public Collider safePlatform;
    [Min(0.1f)] public float lookAhead = 1.1f;
    [Min(0.1f)] public float rayLength = 3.0f;

    [Header("Movement")]
    [Min(0.1f)] public float forwardSpeed = 2.4f;
    [Min(1f)] public float turnDegreesPerSecond = 180f;
    [Min(0f)] public float movingFitnessWeight = 0.55f;

    public DNA dna { get; private set; }
    public bool isAlive { get; private set; }
    public float timeAlive { get; private set; }
    public float distanceMoved { get; private set; }
    public float Fitness => timeAlive + movingFitnessWeight * distanceMoved;

    private Rigidbody body;
    private Vector3 previousPosition;
    private int previousAction = -1;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
    }

    public void Init(DNA genome, Collider ground)
    {
        dna = genome != null ? genome.Copy() : DNA.RandomGenome();
        safePlatform = ground;
        isAlive = true;
        timeAlive = 0f;
        distanceMoved = 0f;
        previousPosition = body.position;
        previousAction = -1;
    }

    private bool SenseGroundAhead()
    {
        if (eyes == null || safePlatform == null) return false;
        Vector3 origin = eyes.position + transform.forward * lookAhead;
        // TODO C: raycast vertically DOWN from origin; confirm hit.collider == safePlatform.
        // Starter returns true, so the bot does not yet react to the edge.
        bool found = true;
        Debug.DrawRay(origin, Vector3.down * rayLength, found ? Color.green : Color.red);
        return found;
    }

    private void FixedUpdate()
    {
        if (!isAlive || dna == null) return;

        // Record ACTUAL horizontal displacement from the previous physics step.
        Vector3 actualDelta = body.position - previousPosition;
        if (previousAction == 0)
        {
            actualDelta.y = 0f;
            distanceMoved += actualDelta.magnitude;
        }
        previousPosition = body.position;

        if (body.position.y < -5f)
        {
            Die();
            return;
        }

        bool groundAhead = SenseGroundAhead();
        int action = dna.GetGene(groundAhead ? 0 : 1);
        previousAction = action;

        if (action == 0)
        {
            body.MovePosition(body.position + transform.forward * forwardSpeed * Time.fixedDeltaTime);
        }
        else
        {
            float direction = action == 1 ? -1f : 1f;
            Quaternion delta = Quaternion.Euler(0f, direction * turnDegreesPerSecond * Time.fixedDeltaTime, 0f);
            body.MoveRotation(body.rotation * delta);
        }
        timeAlive += Time.fixedDeltaTime;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (collision.collider.GetComponent<DeathZone>() != null) Die();
    }

    private void Die()
    {
        if (!isAlive) return;
        isAlive = false;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        // Preserve timeAlive: it is the survival-time fitness component.
    }

    public void EndTrial(float duration)
    {
        if (isAlive) timeAlive = duration;
    }
}
