using Godot;

public partial class PlayerController : CharacterBody3D {
	[Export] public bool canMove = true;
	[Export] public bool hasGravity = true;
	[Export] public bool canJump = true;
	[Export] public bool canSprint = true;
	[Export] public bool canNoclip = false;

	// -----------------------
	// Speeds
	// -----------------------
	[Export] public float lookSpeed = 0.002f;
	[Export] public float baseSpeed = 5.0f;
	[Export] public float jumpVelocity = 4.5f;
	[Export] public float sprintSpeed = 8.0f;

	// -----------------------
	// Input Actions
	// -----------------------
	[Export] public string inputLeft    = "move_left";
	[Export] public string inputRight   = "move_right";
	[Export] public string inputForward = "move_forward";
	[Export] public string inputBack    = "move_back";
	[Export] public string inputUp      = "move_up";
	[Export] public string inputDown    = "move_down";
	[Export] public string inputSprint  = "sprint";
	[Export] public string inputNoclip = "noclip";

	private bool isNoclip = false;
	private bool mouseCaptured = false;
	private Vector2 lookRotation = Vector2.Zero;
	private float moveSpeed = 0.0f;

	private Node3D head;
	private CollisionShape3D collider;

	private Camera3D camera3D;
	private Node3D bodyMesh; 
	private Transform3D previousBodyTransform;
	private Transform3D currentBodyTransform;

	public override void _Ready() {
		head = GetNode<Node3D>("Head");
		collider = GetNode<CollisionShape3D>("Collider");

		camera3D = head.GetNode<Camera3D>("Camera3D");
		bodyMesh = GetNode<Node3D>("Mesh");

		CheckInputMappings();

		lookRotation.Y = Rotation.Y; 
		lookRotation.X = head.Rotation.X;

		previousBodyTransform = GlobalTransform;
		currentBodyTransform = GlobalTransform;
	}

	public override void _UnhandledInput(InputEvent @event) {
		if (Input.IsMouseButtonPressed(MouseButton.Left))
			CaptureMouse();
		if (Input.IsKeyPressed(Key.Escape))
			ReleaseMouse();

		if (mouseCaptured && @event is InputEventMouseMotion mouseMotion)
			RotateLook(mouseMotion.Relative);
	}

	public override void _PhysicsProcess(double delta) {

		if (canNoclip && Input.IsActionJustPressed(inputNoclip)) {
			isNoclip = !isNoclip;

			if (isNoclip) {
				collider.Disabled = true;
			} else {
				collider.Disabled = false;
			}
		}

		if (!isNoclip) {
			// Apply gravity
			if (hasGravity && !IsOnFloor())
				Velocity += GetGravity() * (float)delta;

			// Jump (using inputUp)
			if (canJump && IsOnFloor() && Input.IsActionJustPressed(inputUp))
				Velocity = new Vector3(Velocity.X, jumpVelocity, Velocity.Z);

			// Sprint speed
			if (canSprint && Input.IsActionPressed(inputSprint))
				moveSpeed = sprintSpeed;
			else
				moveSpeed = baseSpeed;

			// Movement
			if (canMove) {
				Vector2 inputDir = Input.GetVector(inputLeft, inputRight, inputForward, inputBack);
				Vector3 moveDir = (Transform.Basis * new Vector3(inputDir.X, 0, inputDir.Y)).Normalized();

				if (moveDir.Length() > 0.001f) {
					Velocity = new Vector3(moveDir.X * moveSpeed, Velocity.Y, moveDir.Z * moveSpeed);
				} else {
					// Lerp velocity down
					float newX = Mathf.MoveToward(Velocity.X, 0.0f, moveSpeed);
					float newZ = Mathf.MoveToward(Velocity.Z, 0.0f, moveSpeed);
					Velocity = new Vector3(newX, Velocity.Y, newZ);
				}
			} else {
				// Movement disabled
				Velocity = new Vector3(0, Velocity.Y, 0);
			}

			MoveAndSlide();
		} 
		else {
			// Freecam-style noclip movement
			float currentSpeed = (canSprint && Input.IsActionPressed(inputSprint)) ? sprintSpeed*1.5f : baseSpeed*1.5f;

			Vector3 camForward = -camera3D.GlobalTransform.Basis.Z; 
			Vector3 camRight   =  camera3D.GlobalTransform.Basis.X;
			Vector3 camUp      =  camera3D.GlobalTransform.Basis.Y;

			Vector3 moveDir = Vector3.Zero;

			// Forward/back
			if (Input.IsActionPressed(inputForward))
					moveDir += camForward;
			if (Input.IsActionPressed(inputBack))
					moveDir -= camForward;

			// Left/right
			if (Input.IsActionPressed(inputLeft))
					moveDir -= camRight;
			if (Input.IsActionPressed(inputRight))
					moveDir += camRight;

			// Up/down
			if (Input.IsActionPressed(inputUp))
					moveDir += camUp;
			if (Input.IsActionPressed(inputDown))
					moveDir -= camUp;

			if (moveDir.Length() > 0.01f)
					moveDir = moveDir.Normalized();

			GlobalTransform = GlobalTransform.Translated(moveDir * currentSpeed * (float)delta);
		}

		// Save transforms for interpolation
		previousBodyTransform = currentBodyTransform;
		currentBodyTransform = GlobalTransform;
	}

	public override void _Process(double delta) {
		// Interpolate the body transform between the last and current physics frames
		float alpha = (float)Engine.GetPhysicsInterpolationFraction();
		Transform3D interpolatedTransform = previousBodyTransform.InterpolateWith(currentBodyTransform, alpha);
		
		camera3D.GlobalTransform = interpolatedTransform * head.Transform;
		bodyMesh.GlobalTransform = interpolatedTransform * head.Transform;
	}

	private void RotateLook(Vector2 rotInput) {
		lookRotation.X -= rotInput.Y * lookSpeed;
		lookRotation.X = Mathf.Clamp(lookRotation.X, Mathf.DegToRad(-85), Mathf.DegToRad(85));
		lookRotation.Y -= rotInput.X * lookSpeed;

		Rotation = new Vector3(Rotation.X, lookRotation.Y, Rotation.Z);
		head.Rotation = new Vector3(lookRotation.X, head.Rotation.Y, head.Rotation.Z);
	}

	private void CaptureMouse() {
		Input.MouseMode = Input.MouseModeEnum.Captured;
		mouseCaptured = true;
	}

	private void ReleaseMouse() {
		Input.MouseMode = Input.MouseModeEnum.Visible;
		mouseCaptured = false;
	}

	private void CheckInputMappings() {
		if (canMove && !InputMap.HasAction(inputLeft)) {
			GD.PushError("Movement disabled. No InputAction found for inputLeft: " + inputLeft);
			canMove = false;
		}
		if (canMove && !InputMap.HasAction(inputRight)) {
			GD.PushError("Movement disabled. No InputAction found for inputRight: " + inputRight);
			canMove = false;
		}
		if (canMove && !InputMap.HasAction(inputForward)) {
			GD.PushError("Movement disabled. No InputAction found for inputForward: " + inputForward);
			canMove = false;
		}
		if (canMove && !InputMap.HasAction(inputBack)) {
			GD.PushError("Movement disabled. No InputAction found for inputBack: " + inputBack);
			canMove = false;
		}

		if (canJump && !InputMap.HasAction(inputUp)) {
			GD.PushError("Jumping disabled. No InputAction found for inputUp: " + inputUp);
			canJump = false;
		}
		
		if (canSprint && !InputMap.HasAction(inputSprint)) {
			GD.PushError("Sprinting disabled. No InputAction found for inputSprint: " + inputSprint);
			canSprint = false;
		}
		if (canNoclip && !InputMap.HasAction(inputNoclip)) {
			GD.PushError("Noclip disabled. No InputAction found for inputNoclip: " + inputNoclip);
			canNoclip = false;
		}
		
		// Check for move_down if needed
		if (!InputMap.HasAction(inputDown)) {
			GD.PushWarning("No InputAction found for inputDown: " + inputDown + ". Downward movement in noclip won't work.");
		}
	}
}
