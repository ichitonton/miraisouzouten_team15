using Unity.Netcode;
using UnityEngine;

public class JointLiner : NetworkBehaviour
{
    private ConfigurableJoint joint;
    Vector3[] ancors = new Vector3[4];
    private LineRenderer line;
    [SerializeField] Material _material; // オブジェクトの半分の高さ
    bool _haveJoint = false;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        line = gameObject.AddComponent<LineRenderer>();
        line.positionCount = 5;
        line.startWidth = 0.05f;
        line.endWidth = 0.05f;
        line.material = _material;  // マテリアルの色を白にする

        if (IsServer)
        {
            Invoke("SetRigidFalse", 0.1f);
        }
        else
        {

            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = true; // クライアントでは物理演算しない
        }


    }

    void SetRigidFalse()
    {
            Rigidbody rb = GetComponent<Rigidbody>();
            rb.isKinematic = false; // クライアントでは物理演算しない
    }

    private void Update()
    {
        if (_haveJoint)
        {
            Line();
        }
    }


    public void SetHaveJoint(bool haveJoint)
    {
        _haveJoint = haveJoint;
    }

    void Line()
    {
        for (int j = 0; j < 2; j++)
        {
            joint = GetComponents<ConfigurableJoint>()[j];
            ancors[j * 2] = joint.transform.TransformPoint(joint.anchor);
            ancors[j * 2 + 1] = joint.connectedBody.transform.TransformPoint(joint.connectedAnchor);

            //網の上を少し下げる（当たり判定には影響なし）
            if (j < 1)
            {
                ancors[j * 2] -= Vector3.up * 0.2f;
                ancors[j * 2 + 1] -= Vector3.up * 0.2f;
            }
            if (joint == null || joint.connectedBody == null)
            {
                line.enabled = false;
                return;
            }
            line.enabled = true;
        }
        // 線を描画
        //[1]  [0]
        //   ×
        //[3]－[2]
        //line.SetPosition(0, ancors[0]);
        //line.SetPosition(1, ancors[3]);
        //line.SetPosition(2, ancors[2]);
        //line.SetPosition(3, ancors[1]);
        //line.SetPosition(4, ancors[0]);
        UpdateLineClientRpc(ancors);

    }
    [ClientRpc]
    void UpdateLineClientRpc(Vector3[] pos)
    {
        if (line == null)
        {
            Debug.LogWarning("[Client] LineRenderer がまだ存在しない。RPC をスキップ");
            return;
        }
        line.SetPosition(0, pos[0]);
        line.SetPosition(1, pos[3]);
        line.SetPosition(2, pos[2]);
        line.SetPosition(3, pos[1]);
        line.SetPosition(4, pos[0]);
    }
}
