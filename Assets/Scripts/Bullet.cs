using TMPro;
using UnityEngine;

namespace L8000
{
    /// <summary>「・」の弾。当たり判定は描画される点だけ(小さな CircleCollider2D)。</summary>
    [DisallowMultipleComponent]
    public class Bullet : MonoBehaviour
    {
        Weapon _weapon;
        Rigidbody2D _rb;
        TextMeshPro _tmp;
        float _startY;
        float _maxRange;
        bool _live;

        public void Init(Weapon weapon, TMP_FontAsset font, float radius, float visualHeight)
        {
            _weapon = weapon;

            _rb = gameObject.AddComponent<Rigidbody2D>();
            _rb.bodyType = RigidbodyType2D.Dynamic;
            _rb.gravityScale = 0f;
            _rb.freezeRotation = true;
            _rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;

            var col = gameObject.AddComponent<CircleCollider2D>();
            col.radius = radius;
            col.isTrigger = true;

            var go = new GameObject("dot", typeof(TextMeshPro));
            go.transform.SetParent(transform, false);
            _tmp = go.GetComponent<TextMeshPro>();
            _tmp.font = font;
            _tmp.fontSize = 32f;
            _tmp.richText = false;
            _tmp.alignment = TextAlignmentOptions.Midline;
            _tmp.color = new Color(1f, 0.85f, 0.2f);   // 明るい黄色でよく見えるように
            _tmp.text = "●";                            // 「・」の視認版(丸い点)
            _tmp.sortingOrder = 30;
            _tmp.ForceMeshUpdate();

            float h = visualHeight;
            var ti = _tmp.textInfo;
            if (ti.characterCount > 0)
            {
                var ch = ti.characterInfo[0];
                float gh = ch.topRight.y - ch.bottomLeft.y;
                if (gh > 0.0001f) _tmp.transform.localScale = Vector3.one * (visualHeight / gh);
                // 見た目の中心を弾の中心に合わせる
                float cy = (ch.topRight.y + ch.bottomLeft.y) * 0.5f * _tmp.transform.localScale.y;
                _tmp.transform.localPosition = new Vector3(0f, -cy, 0f);
            }
        }

        public void Launch(Vector2 velocity, float maxRange)
        {
            _startY = transform.position.y;
            _maxRange = maxRange;
            _rb.linearVelocity = velocity;
            _live = true;
            _lastLine = null;
        }

        void Update()
        {
            if (_live && transform.position.y - _startY > _maxRange) Despawn();
        }

        CreditLineBuilder _lastLine;

        void OnTriggerEnter2D(Collider2D other)
        {
            if (!_live) return;
            var line = other.GetComponentInParent<CreditLineBuilder>();
            if (line == null || line == _lastLine) return;
            _lastLine = line;
            line.HitAt(transform.position);
            Despawn();   // 1発 = 1行だけ。貫通しない(狙って撃つ)
        }

        void Despawn()
        {
            _live = false;
            if (_rb != null) _rb.linearVelocity = Vector2.zero;
            if (_weapon != null) _weapon.Recycle(this);
            else gameObject.SetActive(false);
        }
    }
}
