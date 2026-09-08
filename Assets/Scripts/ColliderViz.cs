using System.Collections.Generic;
using UnityEngine;

namespace L8000
{
    /// <summary>
    /// 当たり判定の可視化。F1 でトグル。
    /// 有効な BoxCollider2D すべてに半透明の矩形を重ねる(足場=赤 / プレイヤー=青 / ===== =橙)。
    /// プール方式・毎フレーム位置だけ更新なので軽い。
    /// </summary>
    public class ColliderViz : MonoBehaviour
    {
        public KeyCode toggleKey = KeyCode.F1;
        public bool visible;

        Sprite _sprite;
        readonly List<SpriteRenderer> _pool = new List<SpriteRenderer>();
        Transform _root;

        void Awake()
        {
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _root = new GameObject("ColliderViz_Pool").transform;
            _root.SetParent(transform, false);
        }

        void Update()
        {
            if (Input.GetKeyDown(toggleKey)) visible = !visible;

            if (!visible)
            {
                if (_pool.Count > 0 && _pool[0].gameObject.activeSelf)
                    foreach (var sr in _pool) sr.gameObject.SetActive(false);
                return;
            }

            int i = 0;
            foreach (var col in FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None))
            {
                if (!col.enabled || !col.gameObject.activeInHierarchy) continue;
                var b = col.bounds;
                var sr = Get(i++);
                sr.transform.position = new Vector3(b.center.x, b.center.y, -3f);
                sr.transform.localScale = new Vector3(Mathf.Max(0.02f, b.size.x), Mathf.Max(0.02f, b.size.y), 1f);

                var pc = col.GetComponent<PlayerController>();
                var line = col.GetComponent<CreditLineBuilder>();
                bool rule = line != null && line.Spec != null && line.Spec.Kind == LineKind.Rule;
                sr.color = pc != null ? new Color(0.3f, 0.8f, 1f, 0.5f)
                         : rule ? new Color(1f, 0.75f, 0.25f, 0.4f)
                         : new Color(1f, 0.3f, 0.3f, 0.34f);
            }
            for (; i < _pool.Count; i++) _pool[i].gameObject.SetActive(false);
        }

        SpriteRenderer Get(int i)
        {
            if (i < _pool.Count) { _pool[i].gameObject.SetActive(true); return _pool[i]; }
            var go = new GameObject("cbox");
            go.transform.SetParent(_root, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _sprite;
            sr.sortingOrder = 60;
            _pool.Add(sr);
            return sr;
        }
    }
}
