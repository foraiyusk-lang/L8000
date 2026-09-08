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
        BoxCollider2D[] _cols;
        float _rescan;
        Camera _cam;

        void Awake()
        {
            _sprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
            _root = new GameObject("ColliderViz_Pool").transform;
            _root.SetParent(transform, false);
            _cam = Camera.main;
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

            // コライダ一覧は毎フレームではなく時々だけ取り直す(タイルが多いので)
            _rescan -= Time.unscaledDeltaTime;
            if (_cols == null || _rescan <= 0f)
            {
                _cols = FindObjectsByType<BoxCollider2D>(FindObjectsSortMode.None);
                _rescan = 0.5f;
            }
            if (_cam == null) _cam = Camera.main;
            float camY = _cam != null ? _cam.transform.position.y : 0f;
            float span = _cam != null ? _cam.orthographicSize + 3f : 12f;

            int i = 0;
            foreach (var col in _cols)
            {
                if (col == null || !col.enabled || !col.gameObject.activeInHierarchy) continue;
                var b = col.bounds;
                if (Mathf.Abs(b.center.y - camY) > span) continue;   // 画面外は描かない
                var sr = Get(i++);
                sr.transform.position = new Vector3(b.center.x, b.center.y, -3f);
                sr.transform.localScale = new Vector3(Mathf.Max(0.02f, b.size.x), Mathf.Max(0.02f, b.size.y), 1f);

                var pc = col.GetComponent<PlayerController>();
                var tile = col.GetComponentInParent<CreditTile>();
                bool rule = tile != null && tile.isRule;
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
