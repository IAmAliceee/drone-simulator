public class PIDController
{
    private float Kp;
    private float Ki;
    private float Kd;
    private float integral;
    private float last_error;

    public PIDController(float kp, float ki, float kd)
    {
        this.Kp = kp;
        this.Ki = ki;
        this.Kd = kd;
        this.integral = 0;
        this.last_error = 0;
    }

    public float UpdatePID(float target, float current, float dt)
    {
        float error = target - current;
        this.integral += last_error * dt;
        float derivative = dt > 0 ? (error - this.last_error) / dt : 0f;
        this.last_error = error;

        return this.Kp * error + this.Ki * this.integral + this.Kd * derivative;
    }
}
