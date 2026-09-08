using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// Enter で「・」を真上に発射する。当たった1文字が消える。弾はプール。
    /// </summary>
    public class Weapon : MonoBehaviour
    {
        public KeyCode fireKey = KeyCode.Return;
        public TMP_FontAsset font;

        [Header("弾")]
        public float bulletSpeed = 9f;
        public float fireCooldown = 0.22f;
        public float bulletMaxRange = 40f;
        public float bulletColliderRadius = 0.22f;
        public float bulletVisualHeight = 0.5f;
        public float muzzleOffsetY = 0.35f;

        /// <summary>検証用: true の間、自動で撃ち続ける。</summary>
        public static bool DebugAutoFire;

        float _cd;
        readonly Stack<Bullet> _pool = new Stack<Bullet>();

        void Update()
        {
            _cd = Mathf.Max(0f, _cd - Time.deltaTime);
            if (_cd <= 0f && (Input.GetKeyDown(fireKey) || DebugAutoFire))
            {
                Fire();
                _cd = fireCooldown;
            }
        }

        public void Fire()
        {
            var b = Rent();
            b.transform.position = transform.position + new Vector3(0f, muzzleOffsetY, 0f);
            b.Launch(new Vector2(0f, bulletSpeed), bulletMaxRange);
        }

        Bullet Rent()
        {
            if (_pool.Count > 0)
            {
                var x = _pool.Pop();
                x.gameObject.SetActive(true);
                return x;
            }
            var go = new GameObject("Bullet");
            var b = go.AddComponent<Bullet>();
            b.Init(this, font, bulletColliderRadius, bulletVisualHeight);
            return b;
        }

        public void Recycle(Bullet b)
        {
            b.gameObject.SetActive(false);
            _pool.Push(b);
        }
    }
}
