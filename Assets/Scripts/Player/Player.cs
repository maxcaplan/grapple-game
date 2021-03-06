﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Controller2D))]
[RequireComponent(typeof(Animator))]
[RequireComponent(typeof(SpriteRenderer))]
[RequireComponent(typeof(LineRenderer))]
[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(DistanceJoint2D))]
public class Player : MonoBehaviour
{
    [Header("Jumping")]
    public float maxJumpHeight = 2;
    public float minJumpHeight = 1;
    public float timeToJumpApex = .4f;

    [Header("Move Speed")]
    public float walkSpeed = 6;
    public float crouchMoveSpeed = 3;

    [Header("Acceleration")]
    public float walkAccelerationAirborne = .2f;
    public float walkAccelerationGrounded = .1f;
    public float crouchAcceleration = .05f;

    [Header("Deacceleration")]
    public float walkDeaccelerationAirborne = .1f;
    public float walkDeaccelerationGrounded = .05f;
    public float crouchedDeacceleration = .05f;
    public float movingToCrouchDeacceleration = 0.25f;
    public float momentumDeacceleraionAirborne = 0.5f;
    public float momentumDeacceleraionGrounded = 0.25f;

    [Header("Aiming")]
    public float minAimDistance = 1f;
    public float maxAimDistance = 10f;
    public Color canShootColor = Color.green;
    public Color cantShootColor = Color.red;
    public LayerMask grappleLayer;

    [Header("Grappling")]
    public float retractionSpeed = 0.1f;
    public float extensionSpeed = 0.1f;

    [Header("Game Objects")]
    public GameObject aimPivot;
    public GameObject aimRetical;
    public GameObject aimSpot;

    float gravity;
    float maxJumpVelocity;
    float minJumpVelocity;

    float targetVelocityX;
    float velocityXSmoothing;
    Vector3 velocity;
    Vector3 swingVelocity;

    bool crouching = false;
    bool previousCrouchState = false;

    float aimRotation;
    Vector2 aimDirection;
    RaycastHit2D aimPosition;

    bool canShoot = false;
    bool ropeAttached = false;

    float targetRopeDistance;

    Controller2D controller;
    SpriteRenderer sprite;
    Animator animator;
    LineRenderer rope;
    Rigidbody2D rb;
    DistanceJoint2D joint;

    void Start()
    {
        controller = GetComponent<Controller2D>();
        sprite = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
        rope = GetComponent<LineRenderer>();
        rb = GetComponent<Rigidbody2D>();
        joint = GetComponent<DistanceJoint2D>();

        rope.enabled = false;
        rb.constraints = RigidbodyConstraints2D.FreezeAll;
        joint.enabled = false;
        joint.anchor = aimPivot.transform.position;

        // Calculate gravity and jump velocity
        gravity = -(2 * maxJumpHeight) / Mathf.Pow(timeToJumpApex, 2);
        maxJumpVelocity = Mathf.Abs(gravity) * timeToJumpApex;
        minJumpVelocity = Mathf.Sqrt(2 * Mathf.Abs(gravity) * minJumpHeight);
    }

    void Update()
    {
        // Reset y velocity on vertical collisions
        if (controller.collisions.above || controller.collisions.below || ropeAttached) velocity.y = 0;

        Vector2 input = new Vector2(Input.GetAxisRaw("Horizontal"), Input.GetAxisRaw("Vertical"));

        // Calculate x y velocity for inputs
        CalculateJump();
        CalculateXVelocity(input);

        // Set crouching state
        UpdateCrouchState(input, velocity);

        // Add gravity to velocity
        velocity.y += gravity * Time.deltaTime;

        // Handle grappling
        UpdateAim();
        UpdateGrapple();
        UpdateGrappleMovment(input);

        // Update Aiming
        CalculateAimDirection();
        aimPivot.transform.eulerAngles = new Vector3(aimPivot.transform.eulerAngles.x, aimPivot.transform.eulerAngles.y, aimRotation);

        // Move player
        controller.Move(((!ropeAttached) ? velocity : (Vector3)rb.velocity) * Time.deltaTime, false, ropeAttached, aimPosition.point);

        // Update Animation
        UpdateAnimation(input);

        aimRetical.SetActive(!ropeAttached);
    }

    void UpdateCrouchState(Vector2 input, Vector3 velocity)
    {
        previousCrouchState = crouching;
        crouching = (ropeAttached) ? false : (input.y < -0.5 && controller.collisions.below);

        // If player is uncrouching, check that there is room to.
        if (previousCrouchState && !crouching)
        {
            crouching = controller.CheckUncrouch(velocity * Time.deltaTime);
        }

        controller.SetCrouchCollisions(crouching);

        // // If crouch state changed, update raycast spacing
        if (previousCrouchState != crouching)
        {
            controller.CalculateRaySpacing();
        }
    }

    // Sets jump velocity based on input
    void CalculateJump()
    {
        if (Input.GetButtonDown("Jump") && controller.collisions.below)
        {
            velocity.y = maxJumpVelocity;
        }
        if (Input.GetButtonUp("Jump") && velocity.y > minJumpVelocity)
        {
            velocity.y = minJumpVelocity;
        }
    }

    // Sets x velocity based on input
    void CalculateXVelocity(Vector2 input)
    {
        float accelerationTime;
        float deaccelerationTime;
        float smoothingTime;

        if (!crouching)
        {
            targetVelocityX = input.x * walkSpeed;
            accelerationTime = (controller.collisions.below) ? walkAccelerationGrounded : walkAccelerationAirborne;
            deaccelerationTime = (controller.collisions.below) ? walkDeaccelerationGrounded : walkDeaccelerationAirborne;
            smoothingTime = (targetVelocityX != 0) ? accelerationTime : deaccelerationTime;
        }
        else
        {
            targetVelocityX = input.x * crouchMoveSpeed;
            accelerationTime = crouchAcceleration;
            deaccelerationTime = crouchedDeacceleration;
            smoothingTime = (targetVelocityX != 0) ? accelerationTime : deaccelerationTime;
            smoothingTime = (Mathf.Abs(targetVelocityX) < Mathf.Abs(velocity.x)) ? movingToCrouchDeacceleration : smoothingTime;
        }

        if (input.x != 0 && Mathf.Abs(velocity.x) > Mathf.Abs(targetVelocityX) && Mathf.Abs(velocity.x) > walkSpeed)
        {
            smoothingTime = (controller.collisions.below) ? momentumDeacceleraionGrounded : momentumDeacceleraionAirborne;
        }

        velocity.x = Mathf.SmoothDamp(velocity.x, targetVelocityX, ref velocityXSmoothing, smoothingTime);
    }

    // Calculates direction and angle between aim pivot and mouse
    void CalculateAimDirection()
    {
        Vector3 mousePos = Input.mousePosition;
        mousePos.z = Camera.main.nearClipPlane;
        Vector2 worldPosition = Camera.main.ScreenToWorldPoint(mousePos);

        aimDirection = worldPosition - (Vector2)transform.position;
        aimRotation = Mathf.Atan2(aimDirection.y, aimDirection.x) * Mathf.Rad2Deg;
    }

    void UpdateAim()
    {
        if (ropeAttached) return;

        RaycastHit2D hit = Physics2D.Raycast(aimPivot.transform.position, aimDirection.normalized, maxAimDistance, grappleLayer);
        Debug.DrawRay(aimPivot.transform.position, aimDirection.normalized * maxAimDistance, Color.green);

        if (hit)
        {
            canShoot = (hit.distance > minAimDistance);
            aimPosition = hit;

            aimSpot.transform.position = hit.point;
            aimSpot.GetComponent<SpriteRenderer>().color = (canShoot) ? canShootColor : cantShootColor;
            aimSpot.SetActive(true);
        }
        else
        {
            aimSpot.SetActive(false);
            canShoot = false;
        }
    }

    void UpdateGrapple()
    {
        if (Input.GetButton("Fire1") && canShoot)
        {
            // Enable and update rope rendering
            rope.enabled = true;
            rope.SetPosition(0, aimPivot.transform.position);
            rope.SetPosition(1, aimPosition.point);

            // Enable and set joint
            joint.connectedAnchor = aimPosition.point;
            joint.distance = Vector2.Distance(aimPivot.transform.position, aimPosition.point);
            joint.enabled = true;

            // Unfreese rigidbody
            rb.constraints = RigidbodyConstraints2D.None;
            rb.freezeRotation = true;

            Vector2 ropeDir = (aimPosition.point - (Vector2)aimPivot.transform.position).normalized;
            Vector2 perpendicularVelocity = Vector2.Perpendicular(ropeDir).normalized;

            float direction = -Mathf.Sign(velocity.x);
            if (Mathf.Abs(velocity.x) <= 0.01)
            {
                direction = (aimPivot.transform.position.x > aimPosition.point.x) ? 1 : -1;
                // direction *= -Mathf.Sign(velocity.y);
            }

            Debug.DrawRay(aimPivot.transform.position, perpendicularVelocity * Mathf.Abs(velocity.magnitude) * direction, Color.yellow);

            if (!ropeAttached)
            {
                if (Mathf.Abs(velocity.x) <= 0.01) print("no x velocity");

                rb.AddForce(perpendicularVelocity * Mathf.Abs(velocity.magnitude) * direction, ForceMode2D.Impulse);
            }

            // Set rope state
            ropeAttached = true;

        }
        else
        {
            // Disable rope and physics
            rope.enabled = false;

            // Update plauers velocity when leaving rope
            if (ropeAttached) velocity = rb.velocity;

            joint.enabled = false;
            rb.constraints = RigidbodyConstraints2D.FreezeAll;

            ropeAttached = false;
        }
    }

    void UpdateGrappleMovment(Vector2 input)
    {
    }

    void UpdateAnimation(Vector2 input)
    {
        if (input.x != 0)
        {
            sprite.flipX = (Mathf.Sign(input.x) == -1);
        }
        animator.SetFloat("velocityX", Mathf.Abs(targetVelocityX));
        animator.SetFloat("velocityY", velocity.y);
        animator.SetBool("grounded", controller.collisions.below);
        animator.SetBool("crouching", crouching);
    }
}
