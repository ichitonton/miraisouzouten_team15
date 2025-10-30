using UnityEngine;

namespace ExplosionSample
{
    public class ShellBomb : MonoBehaviour
    {
        [Header("”š”­‚Ü‚Å‚ÌŠÔ[s]")]
        [SerializeField]
        private float _time = 3.0f;

        [Header("”š•—‚ÌPrefab")][SerializeField] private Explosion _explosionPrefab;

        private void Start()
        {
            // ˆê’èŠÔŒo‰ßŒã‚É”­‰Î
            Invoke(nameof(Explode), _time);
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (collision.gameObject.tag == "Player")
            {
                Explode();
            }
        }

        private void Explode()
        {
            // ”š”­‚ğ¶¬
            var explosion = Instantiate(_explosionPrefab, transform.position, Quaternion.identity);
            explosion.Explode();

            // ©g‚ÍÁ‚¦‚é
            Destroy(gameObject);
        }
    }
}
