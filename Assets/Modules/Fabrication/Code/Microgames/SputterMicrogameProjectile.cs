using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SpaceFab.Fabrication.Microgames
{
    public class SputterMicrogameProjectile : MonoBehaviour
    {
        public SpriteRenderer Sprite;
        public Rigidbody2D Rigidbody;
        private float Speed = 5f;
        private float InitialAngle = 0;
        private bool Reflected = false;

        public void SetDirection(float angle)
        {
            InitialAngle = angle;
            Vector2 direction = Quaternion.Euler(0, 0, InitialAngle) * Vector2.right;
            Rigidbody.velocity = direction * Speed;
        }

        // Spawns this particle already past the reflection point, as part of a shower burst.
        public void InitializeAsShowerParticle(float angle, float speed)
        {
            InitialAngle = angle;
            Vector2 direction = Quaternion.Euler(0, 0, InitialAngle) * Vector2.right;
            Rigidbody.velocity = direction * speed;
            Reflected = true;
        }

        private void OnTriggerEnter2D(Collider2D collision)
        {
            if (collision.gameObject.name == "Mirror")
            {
                // Shower particles are spawned already-reflected, still overlapping the mirror's
                // collider; without this guard each one immediately re-fires this branch and spawns
                // another shower recursively at the same spot instead of flying away.
                if (Reflected) { return; }

                float reflectedAngle = -InitialAngle;
                SputterMicrogameUtility.SpawnParticleShower(transform.position, reflectedAngle);
                Destroy(this.gameObject);
            }
            else if (collision.gameObject.GetComponent<SputterBoxCollider>() == null)
            {
                Destroy(this.gameObject);
            }
        }

        private void OnTriggerStay2D(Collider2D collision)
        {
            if (!Reflected) { return; }

            SputterBoxCollider collider = collision.gameObject.GetComponent<SputterBoxCollider>();
            if (collider != null)
            {
                if (collider.TryFillSlot(this.gameObject))
                {
                    Rigidbody.velocity = Vector2.zero;
                    Rigidbody.simulated = false;
                }
            }
        }
    }
}