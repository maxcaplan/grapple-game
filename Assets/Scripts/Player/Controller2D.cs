using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Controller2D : RayCollisionController
{
    float maxClimbAngle = 80;
    float MaxDescendAngle = 75;

    Vector3 swingVelocity = Vector3.zero;

    public CollisionInfo collisions;

    public Vector2 standingColliderSize = new Vector2(2, 2);
    public Vector2 standingColliderPos = new Vector2();

    public Vector2 crouchingColliderSize = new Vector2(2, 2);
    public Vector2 crouchingColliderPos = new Vector2();

    // Override inherited start function and run it in child
    public override void Start()
    {
        base.Start();
    }

    // Moves the player with input velocity
    public void Move(Vector3 velocity, bool standingOnPlatform = false, bool ropeAttached = false, Vector2 anchorPos = new Vector2())
    {
        UpdateRaycastOrigins();
        collisions.Reset();
        collisions.velocityOld = velocity;

        // Running and walking movment:

        swingVelocity = Vector3.zero;

        if (velocity.y < 0)
        {
            DescendSlope(ref velocity);
        }

        // Check collisions
        if (velocity.x != 0)
        {
            HorizontalCollisions(ref velocity);
        }
        if (velocity.y != 0)
        {
            VerticalCollisions(ref velocity);
        }

        // Move player
        if (!ropeAttached) transform.Translate(velocity);

        if (standingOnPlatform)
        {
            collisions.below = true;
        }
    }

    // Calculate collisions for horizontal rays
    void HorizontalCollisions(ref Vector3 velocity)
    {
        float directionX = Mathf.Sign(velocity.x);
        float rayLength = Mathf.Abs(velocity.x) + skinWidth;

        for (int i = 0; i < horizontalRayCount; i++)
        {
            // Set ray origin to bottom or top based on velocity x direction
            Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight;
            rayOrigin += Vector2.up * (horizontalRaySpacing * i);
            // Cast ray 
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            //Draws horizontal rays for debugging
            Debug.DrawRay(rayOrigin, Vector2.right * directionX * rayLength, Color.red);

            if (hit)
            {
                if (hit.distance == 0)
                {
                    continue;
                }

                // Offset velocity for sloped surfaces
                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                if (i == 0 && slopeAngle <= maxClimbAngle)
                {
                    if (collisions.descendingSlope)
                    {
                        collisions.descendingSlope = false;
                        velocity = collisions.velocityOld;
                    }

                    float distanceToSlopeStart = 0;
                    if (slopeAngle != collisions.slopeAngleOld)
                    {
                        distanceToSlopeStart = hit.distance - skinWidth;
                        velocity.x -= distanceToSlopeStart * directionX;
                    }

                    ClimbSlope(ref velocity, slopeAngle);
                    velocity.x += distanceToSlopeStart * directionX;
                }

                if (!collisions.climbingSlope || slopeAngle > maxClimbAngle)
                {
                    // Modify velocity to end at collision surface
                    velocity.x = (hit.distance - skinWidth) * directionX;
                    // Change raylength so as to only collide with closest surface
                    rayLength = hit.distance;

                    // Adjust y velocity if colliding with wall while climbing a slope
                    if (collisions.climbingSlope)
                    {
                        velocity.y = Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad * Mathf.Abs(velocity.x));
                    }

                    // Set which side of the player collided
                    collisions.left = directionX == -1;
                    collisions.right = directionX == 1;
                }
            }
        }
    }

    // Calculate collisions for vertical rays
    bool VerticalCollisions(ref Vector3 velocity)
    {
        bool collided = false;

        float directionY = Mathf.Sign(velocity.y);
        float rayLength = Mathf.Abs(velocity.y) + skinWidth;

        for (int i = 0; i < verticalRayCount; i++)
        {
            // Set ray origin to bottom or top based on velocity y direction
            Vector2 rayOrigin = (directionY == -1) ? raycastOrigins.bottomLeft : raycastOrigins.topLeft;
            rayOrigin += Vector2.right * (verticalRaySpacing * i + velocity.x);
            // Cast ray 
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.up * directionY, rayLength, collisionMask);

            //Draws vertical rays for debugging
            Debug.DrawRay(rayOrigin, Vector2.up * directionY * rayLength, Color.red);

            if (hit)
            {
                collided = true;

                // Modify velocity to end at collision surface
                velocity.y = (hit.distance - skinWidth) * directionY;
                // Change raylength so as to only collide with closest surface
                rayLength = hit.distance;

                if (collisions.climbingSlope)
                {
                    velocity.x = velocity.y / Mathf.Tan(collisions.slopeAngle * Mathf.Deg2Rad) * Mathf.Sign(velocity.x);
                }

                // Set which side of the plauer collided
                collisions.below = directionY == -1;
                collisions.above = directionY == 1;
            }
        }

        if (collisions.climbingSlope)
        {
            float directionX = Mathf.Sign(velocity.x);
            rayLength = Mathf.Abs(velocity.x + skinWidth);
            Vector2 rayOrigin = ((directionX == -1) ? raycastOrigins.bottomLeft : raycastOrigins.bottomRight) + Vector2.up * velocity.y;
            RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.right * directionX, rayLength, collisionMask);

            if (hit)
            {
                collided = true;

                float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
                if (slopeAngle != collisions.slopeAngle)
                {
                    velocity.x = (hit.distance - skinWidth) * directionX;
                    collisions.slopeAngle = slopeAngle;
                }
            }
        }

        return collided;
    }

    // Calculates x y velocities to move the x distance of the input velocity on an upwards slope
    void ClimbSlope(ref Vector3 velocity, float slopeAngle)
    {
        float moveDistance = Mathf.Abs(velocity.x);
        float climbVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

        if (velocity.y <= climbVelocityY)
        {
            velocity.y = climbVelocityY;
            velocity.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);

            collisions.below = true;
            collisions.climbingSlope = true;
            collisions.slopeAngle = slopeAngle;
        }
    }

    void DescendSlope(ref Vector3 velocity)
    {
        float directionX = Mathf.Sign(velocity.x);
        Vector2 rayOrigin = (directionX == -1) ? raycastOrigins.bottomRight : raycastOrigins.bottomLeft;

        RaycastHit2D hit = Physics2D.Raycast(rayOrigin, Vector2.down, Mathf.Infinity, collisionMask);

        if (hit)
        {
            float slopeAngle = Vector2.Angle(hit.normal, Vector2.up);
            if (slopeAngle != 0 && slopeAngle <= MaxDescendAngle)
            {
                if (Mathf.Sign(hit.normal.x) == directionX)
                {
                    if (hit.distance - skinWidth <= Mathf.Tan(slopeAngle * Mathf.Deg2Rad) * Mathf.Abs(velocity.x))
                    {
                        float moveDistance = Mathf.Abs(velocity.x);
                        float descendVelocityY = Mathf.Sin(slopeAngle * Mathf.Deg2Rad) * moveDistance;

                        velocity.x = Mathf.Cos(slopeAngle * Mathf.Deg2Rad) * moveDistance * Mathf.Sign(velocity.x);
                        velocity.y -= descendVelocityY;

                        collisions.below = true;
                        collisions.slopeAngle = slopeAngle;
                        collisions.descendingSlope = true;
                    }
                }
            }
        }
    }

    // Sets player hitbox for crouching and standing states
    public void SetCrouchCollisions(bool crouching = false)
    {
        if (crouching)
        {
            collider2d.size = crouchingColliderSize;
            collider2d.offset = crouchingColliderPos;
        }
        else
        {
            collider2d.size = standingColliderSize;
            collider2d.offset = standingColliderPos;
        }
    }

    // Checks wether the player can uncrouch
    public bool CheckUncrouch(Vector3 velocity)
    {
        // Check if there is an obstacle less than the standing hitboxes height away
        float crouchTop = crouchingColliderSize.y;
        float standingTop = standingColliderSize.y;
        velocity.y = Mathf.Abs(Mathf.Abs(standingTop) - Mathf.Abs(crouchTop)) - skinWidth * 2;
        bool hit = VerticalCollisions(ref velocity);

        if (hit)
        {
            return true;
        }
        else
        {
            return false;
        }
    }

    public struct CollisionInfo
    {
        public bool above, below;
        public bool left, right;

        public bool climbingSlope, descendingSlope;
        public float slopeAngle, slopeAngleOld;

        public Vector3 velocityOld;

        public void Reset()
        {
            above = below = false;
            left = right = false;

            climbingSlope = false;
            descendingSlope = false;

            slopeAngleOld = slopeAngle;
            slopeAngle = 0;
        }
    }
}
