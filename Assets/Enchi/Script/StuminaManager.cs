using UnityEngine;

public class StaminaManager : MonoBehaviour
{
    [SerializeField] int _maxStamina = 100;
    private int _nowStamina;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _nowStamina = _maxStamina;
    }

    public void StaminaDecrease(int dereaseValue)
    {
        _nowStamina -= dereaseValue;
        if (_nowStamina < 0)
        {
            _nowStamina = 0;
        }
    }

}
