using DigitalRuby.LightningBolt;
using Platformer.Mechanics;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Rune;

[Serializable]
public enum BattleState { START, PLAYER_TURN, ENEMY_TURN, WON, LOST }

//Need to change enum to class, or it needs ot have unique damage
public enum CombatOptions
{
    Stun = 5,
    Heal = 15,
    Knife = 10,
    Slam = 12,
    Electrocute = 14,
    Fireball = 16,
    // element combo spells
    FireElement = 6,
    EarthElement = 7,
    WaterElement = 8,
    ElementalInfluence = 0
}

public class TurnActions {
    public CombatOptions action;
    public float waitTime;
    public Func<bool, GameObject> actionFunc;
    public TurnActions(CombatOptions co, float f, Func<bool, GameObject> a)
    {
        action = co;
        waitTime = f;
        actionFunc = a;
    }
}

public class BattleSystem : MonoBehaviour
{
    public float dialogWaitTime = 2f;

    GameObject player;
    PlayerHealthBar playerHP;
    RigidbodyConstraints playerRBConstraints;
    GameObject enemy;
    PlayerHealthBar enemyHP;
    [SerializeField] AudioSource winSound;
    [SerializeField] AudioSource loseSound;
    [SerializeField] AudioSource slamSound;
    [SerializeField] AudioSource knifeSound;
    [SerializeField] AudioSource electrocuteSound;
    [SerializeField] AudioSource healSound;
    [SerializeField] AudioSource stunSound;
    [SerializeField] AudioSource dodgeSound;
    [SerializeField] AudioSource fireSound;
    [SerializeField] AudioSource waterSound;
    [SerializeField] AudioSource earthSound;


    public GameObject fireboltAsset;
    public GameObject lightningAsset;
    public GameObject knifeAsset;

  
    public GameObject enemyStunAsset;
    public GameObject stunObj;
    public GameObject healAsset;
    public GameObject healObj;
    public int remaningStunTurns = 0;


    TextMeshProUGUI battleDialog;

    public BattleState state;
    public bool isPlayerFirstTurn;
    private readonly List<Func<string>> battleStartListeners = new();
    private readonly List<Func<string>> playerTurnBeginListeners = new();
    private readonly List<Func<string>> playerTurnEndListeners = new();

    bool playerDodged = false;

    Animator animator;

    Animator enemyAnimator;
    Animator ghostAnimator;
    WaitForSeconds jumpWait = new WaitForSeconds(5.6f); // use to wait for anim to finish
    WaitForSeconds wait3sec = new WaitForSeconds(3f);
    WaitForSeconds wait2sec = new WaitForSeconds(2f);
    WaitForSeconds wait1sec = new WaitForSeconds(1f);
    GameObject ghostBasic;
    GameObject enemyReference;

    readonly List<TurnActions> turnActions = new ();
    public ResourceHandler resourceHandler = new();
    public GameManager.Spell[] spells = new GameManager.Spell[4];
    private int playerPowerBoost = 2;
    private int DialogueCounter = 0;    //Used for player corresponding dialogue options by enemy type

	private int fireCount;
	private int earthCount;
	private int waterCount;
	private int EleInfluenceDamange;
	private TurnbasedDialogHandler turnbasedDialogHandler;

	public void StartCombatRound()
	{
		fireCount = 0;
		earthCount = 0;
		waterCount = 0;
		EleInfluenceDamange = 0;
	}

	private float playerTurnTimer = 11f;
	private bool isTimerStarted = false;
    public bool hasSubmitted = false;
	[SerializeField] TextMeshProUGUI timerText;

	void Update()
	{
		UpdateDifficulty();
		if (state == BattleState.PLAYER_TURN)
		{
			int minutes = Mathf.FloorToInt(playerTurnTimer / 60);
			int seconds = Mathf.FloorToInt(playerTurnTimer % 60);
			if (!isTimerStarted)
			{
				StartPlayerTurnTimer();
				isTimerStarted = true;
			}
			if (playerTurnTimer > 0)
			{
				playerTurnTimer -= Time.deltaTime;
				timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
			}
			else if (!hasSubmitted && playerTurnTimer < 0)
			{
				playerTurnTimer = 0;
				timerText.text = "00:00";
				timerText.color = Color.red;
				SubmitAndEndPlayerTurn();
			}
		}
		else
		{
			isTimerStarted = false;
            timerText.text = "";
		}
	}

    private void SubmitAndEndPlayerTurn()
    {
		turnbasedDialogHandler.SubmitSelected();
	}

    public void StartPlayerTurnTimer()
	{
		timerText.color = Color.white;
		hasSubmitted = false;
		playerTurnTimer = 11f;
	}

	public void StopPlayerTurnTimer()
	{
		playerTurnTimer = 0;
		timerText.text = "";
		hasSubmitted = true;
	}


	void Start()
    {
		animator = GetComponent<Animator>();
        state = BattleState.START;
        player = GameObject.FindWithTag("Player");
        enemy = GameObject.FindWithTag("Enemy");
        playerHP = player.GetComponent<PlayerHealthBar>();
        enemyHP = enemy.GetComponentInChildren<PlayerHealthBar>();
        battleDialog = GameObject.FindWithTag("BattleDialog").GetComponent<TextMeshProUGUI>();
		turnbasedDialogHandler = GetComponentInChildren<TurnbasedDialogHandler>();
		//freeze rotation/position
		playerRBConstraints = player.GetComponentInChildren<Rigidbody>().constraints;
        player.GetComponentInChildren<Rigidbody>().constraints = (RigidbodyConstraints)122;//freeze position xz, rotation

        enemyReference = GameObject.FindWithTag("enemyReference");

        enemyReference.GetComponent<Rigidbody>().constraints = (RigidbodyConstraints)122;

        // Adjust the enemy health based on enemy type
        if (enemyReference.name.ToLower().Contains("skel"))
        {
            enemyHP.healthBar.SetMaxHealth(60);
            enemyHP.currentHealth = 60;
        }
        else if (enemyReference.name.ToLower().Contains("eye"))
        {
            enemyHP.healthBar.SetMaxHealth(75);
            enemyHP.currentHealth = 75;
        }

        enemyAnimator = enemyReference.GetComponent<Animator>(); // enemy animation controller
        ghostBasic = GameObject.Find("ghost basic");
        ghostAnimator = ghostBasic.GetComponent<Animator>();

    }

    private void FixedUpdate()
    {
        battleDialog = GameObject.FindWithTag("BattleDialog").GetComponent<TextMeshProUGUI>();
        enemyReference = GameObject.FindWithTag("enemyReference");
        //Debug.Log(enemyReference.name);

        player.GetComponentInChildren<Rigidbody>().constraints = (RigidbodyConstraints)122;//freeze position xz, rotation
        enemyReference.GetComponent<Rigidbody>().constraints = (RigidbodyConstraints)122;
        enemyAnimator = enemyReference.GetComponent<Animator>();
	    ghostAnimator = ghostBasic.GetComponent<Animator>();
    }

    public void Resume()
    {
        GetComponentInChildren<TurnbasedDialogHandler>().Disable();
        StartCoroutine(SetupBattle());
    }

    /** 
     *  Going to need to move all of the spell creation to the GameManager,
     *  or at least some of it. We need to know the spells the player has
     *  when we replace them.
     */
    public void SetupSpells(string[] selectedSpells)
    {
        for (int i = 0; i < 4; i++)
        {
            Action eventFunc = selectedSpells[i] switch
            {
                "Dodge"       => OnDodgeButton,
                "Electrocute" => OnElectrocuteButton,
                "Fireball"    => OnFireButton,
                "Heal"        => OnHealButton,
                "Slam"        => OnSlamButton,
                "Stun"        => OnStunButton,
                "Knife Throw" => OnKnifeButton,
                "Fire Element" => OnFireEleButton,
                "Earth Element" => OnEarthEleButton,
                "Water Element" => OnWaterEleButton,
                "Elemental Influence" => OnElementalButton,
                _             => null
            };
            
            if (eventFunc != null)
            {
                GameManager.Instance.RegisterSpellEventFunc(selectedSpells[i], eventFunc);
            }
            else {
                Debug.Log("Error: Could not find spell named " + selectedSpells[i]);
            }
            spells[i] = GameManager.Instance.spells.Find(s => s.name.Equals(selectedSpells[i]));
        }
    }

    IEnumerator SetupBattle()
    {
        battleDialog.text = "Combat Start!";
        
        foreach (var fun in battleStartListeners)
        {
            fun();
        }

        yield return new WaitForSeconds(dialogWaitTime);

        if (isPlayerFirstTurn)
            PlayerTurn();
        else
            EnemyTurn();
    }

    void PlayerTurn()
    {
        battleDialog.color = Color.white;
        battleDialog.text = "";
        state = BattleState.PLAYER_TURN;
        foreach (var fun in playerTurnBeginListeners)
        {
            fun();
        }
    }

    IEnumerator ProcessTurn()
    {
        StartCombatRound();
        foreach (var fun in playerTurnEndListeners)
        {
            fun();
        }

        List<TurnActions> elementalInfluences = new List<TurnActions>();
        List<TurnActions> otherActions = new List<TurnActions>();

        foreach (var action in turnActions)
        {
            if (action.action == CombatOptions.ElementalInfluence)
            {
                elementalInfluences.Add(action);
            }
            else
            {
                otherActions.Add(action);
            }
        }

        otherActions.AddRange(elementalInfluences);
        turnActions.Clear();
        turnActions.AddRange(otherActions);

        foreach (var action in turnActions)
        {
            if (action.action == CombatOptions.Heal)
            {
                selfHeal();
            }
            else
            {
                battleDialog.text = "The enemy takes " + action.action.ToString();
                var gameObj = action.actionFunc(true);
                yield return new WaitForSeconds(action.waitTime);
                yield return SpellEffectByEnemy(action);
                GameObject.Destroy(gameObj);
            }

            yield return new WaitForSeconds(dialogWaitTime);
            if (enemyHP.currentHealth <= 0)
            {
                state = BattleState.WON;
                break;
            }
        }
		turnActions.Clear();
		if (state == BattleState.WON)
        {
            StartCoroutine(EndBattle());
        }
        else if (remaningStunTurns < 1)
        {
            state = BattleState.ENEMY_TURN;
            StartCoroutine(EnemyTurn());
        }
        else
        {
            if (--remaningStunTurns == 0)
                Destroy(stunObj);
            PlayerTurn();
        }
    }


    //This Function plays goofy dialogues based on the spells used and also updates the enemy health.
    IEnumerator SpellEffectByEnemy(TurnActions action)
    {
        int enemyNewHP;
        if (action.action == CombatOptions.Slam && enemyReference.name.ToLower().Contains("skel"))
        {
            enemyNewHP = enemyHP.TakeDamage((int)(3.0f*playerPowerBoost/4 * (int)action.action), false);
            GameManager.Instance.foundWeakness("Slam");
            switch (DialogueCounter)
            {
                case 0:
                    DialogueCounter++;
                    battleDialog.color = Color.white;
                    battleDialog.text = "Skeleton's bones rattled";
                    yield return new WaitForSeconds(1.5f);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> Be careful! My bones are brittle from lack of vitamin D";
                    yield return new WaitForSeconds(2.5f);
                    break;
                case 1:
                    DialogueCounter++;
                    battleDialog.color = Color.white;
                    battleDialog.text = "Skeleton's tooth fell out";
                    yield return new WaitForSeconds(1.5f);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> Just my luck, if only we had a dentist in this mansion";
                    yield return new WaitForSeconds(2.5f);
                    break;
                case 2:
                    DialogueCounter++;
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> I got no hair, but I sure got some hairline fractures now";
                    yield return new WaitForSeconds(2.5f);
                    break;
            }
        }
        else if (action.action == CombatOptions.Knife && enemyReference.name.ToLower().Contains("eye"))
        {
            enemyNewHP = enemyHP.TakeDamage((int)(3.0f*playerPowerBoost/4 * (int)action.action), false);
            switch (DialogueCounter)
            {
                case 0:
                    DialogueCounter++;
                    battleDialog.color = Color.white;
                    battleDialog.text = "One of the Monster's eyes popped";
                    yield return new WaitForSeconds(2f);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> I would have been in trouble if not for my 11 other eyes";
                    yield return new WaitForSeconds(2.5f);
                    break;
                case 1:
                    DialogueCounter++;
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> That was my favorite eye!!";
                    yield return new WaitForSeconds(2f);
                    break;
                case 2:
                    DialogueCounter++;
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> Are you in a Knife Throwing Competition, and my eye is the bullseye???";
                    yield return new WaitForSeconds(2.5f);
                    break;
                default:
                    DialogueCounter++;
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> Stop it!! I am gonna run out of eyes!!";
                    yield return new WaitForSeconds(2.5f);
                    break;
            }
        }
        else if (action.action == CombatOptions.Fireball && enemyReference.name.ToLower().Contains("horse"))
        {
            switch (DialogueCounter)
            {
                case 0:
                    DialogueCounter++;
                    enemyNewHP = enemyHP.TakeDamage(playerPowerBoost * 8, false);
                    battleDialog.color = Color.white;
                    battleDialog.text = "The boss started to glow slightly red";
                    yield return new WaitForSeconds(1.7f);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> Who turned on the heater?";
                    yield return new WaitForSeconds(2f);
                    break;
                case 1:
                    DialogueCounter++;
                    enemyNewHP = enemyHP.TakeDamage(playerPowerBoost * 9, false);
                    battleDialog.color = Color.white;
                    battleDialog.text = "The boss turned even more red";
                    yield return new WaitForSeconds(1.7f);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%>  Somebody! Turn on the A/C!";
                    yield return new WaitForSeconds(2f);
                    battleDialog.text = "<size=60%>  Oh wait... Our utility bills have been due for ages";
                    yield return new WaitForSeconds(2f);
                    break;
                case 2:
                    DialogueCounter++;
                    enemyNewHP = enemyHP.TakeDamage(playerPowerBoost * 10, false);
                    battleDialog.color = Color.white;
                    battleDialog.text = "The boss started melting";
                    yield return new WaitForSeconds(1f);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> Go be a pyromaniac somewhere else!";
                    yield return new WaitForSeconds(2.5f);
                    break;
                default:
                    DialogueCounter++;
                    enemyNewHP = enemyHP.TakeDamage(playerPowerBoost * 12, false);
                    battleDialog.color = Color.red;
                    battleDialog.text = "<size=60%> I am about to have a heat stroke!";
                    yield return new WaitForSeconds(2f);
                    break;
            }
        }
		else if (action.action == CombatOptions.ElementalInfluence)
		{
			enemyNewHP = enemyHP.TakeDamage(EleInfluenceDamange, false);
		}
		else
        {
            enemyNewHP = enemyHP.TakeDamage(playerPowerBoost * (int)action.action / 2, false);
        }
	}

    public int getRandomAbilityBasedOnEnemyType()
    {
        var lowerCaseEnemyName = PlayerPrefs.GetString("ObjectToSpawn").ToLower();

        return lowerCaseEnemyName switch
        {
            string enemyName when enemyName.Contains("skeleton") => Time.renderedFrameCount % 50, //skeleton lower 2 abilities: knife or slam
            string enemyName when enemyName.Contains("monster") => Time.renderedFrameCount % 50 + 25, //monster middle 2: slam or fire
            string enemyName when enemyName.Contains("chess") || enemyName.Contains("horse") => Time.renderedFrameCount % 100, //final boss can do all 4 
            _ => 100  //default: electrocute
        };
    }

    IEnumerator EnemyTurn()
    {
        int randomInt = getRandomAbilityBasedOnEnemyType();
        CombatOptions enemyAction = CombatOptions.Knife;
        battleDialog.color = Color.white;
        string dialogText = "The enemy <harm> you";

        if (playerDodged)
        {
            animator.Play("PlayerDodge");
            dodgeSound.Play();
        }


        switch (randomInt)
        {
            case < 25:
                sendKnife(false);
                if (enemyReference.name.ToLower().Contains("skel"))
                {
                    battleDialog.text = dialogText.Replace("<harm>", "threw a swinging sword at");
                    knifeSound.PlayDelayed(1);
                }
                else if (enemyReference.name.ToLower().Contains("horse"))
                {
                    battleDialog.text = dialogText.Replace("<harm>", "threw deadly katanas at");
                    yield return wait1sec;
                }
                else
                {
                    battleDialog.text = dialogText.Replace("<harm>", "threw a knife at");
                    knifeSound.PlayDelayed(2);
                }
                yield return wait1sec;
                break;
            case < 50:
                enemyAction = CombatOptions.Slam;
                battleDialog.text = playerDodged ? "You dodged enemy's slam!" : dialogText.Replace("<harm>", "slammed into");

                if (!playerDodged) { 
                    sendSlam(false);
                    slamSound.PlayDelayed(2.7f);
                }
                    yield return wait3sec; // important for animation to finish
                break;
            case < 75:
                enemyAction = CombatOptions.Fireball;
                battleDialog.text = dialogText.Replace("<harm>", "threw a fireball at");
                sendFireball(false);
                yield return wait1sec; //new WaitForSeconds(1f);
                break;
            default:
                enemyAction = CombatOptions.Electrocute;
                battleDialog.text = dialogText.Replace("<harm>", "electrocutes");
                var lightning = sendLightning();
                yield return wait1sec; //new WaitForSeconds(1f);
                Destroy(lightning);
                break;
        }
        if (!playerDodged || enemyAction is CombatOptions.Electrocute) 
        {   
            var enemyName = PlayerPrefs.GetString("ObjectToSpawn").ToLower();
            var damage = (int)enemyAction * (enemyName.Contains("chess") || enemyName.Contains("horse") ? 2 : 1);//if final boss x2 damage
            damage = damage / (enemyAction is CombatOptions.Electrocute && playerDodged ? 2 : 1);//id dodging electrocute, only 1/2 damage 
            playerHP.TakeDamage(damage, true);
        }
        playerDodged = false;

        yield return new WaitForSeconds(dialogWaitTime);

        if (playerHP.currentHealth <= 0)
        {
            state = BattleState.LOST;
            StartCoroutine(EndBattle());
        }
        else
        {
            state = BattleState.PLAYER_TURN;
            PlayerTurn();
        }
    }

    IEnumerator EndBattle()
    {
        player.GetComponentInChildren<Rigidbody>().constraints = playerRBConstraints;//restore ability to move/rotate
        DialogueCounter = 0;    //Set the dialogue counter back to initial value
        yield return DeathDialogues();
        if (state == BattleState.WON)
        {
            winSound.Play();
            battleDialog.color = Color.white;
            battleDialog.text = "You have prevailed!";
            var lowerCaseEnemyName = PlayerPrefs.GetString("ObjectToSpawn").ToLower();
            if (lowerCaseEnemyName.Contains("skeleton"))
            {
				battleDialog.text += " Coin + 30";
				GameManager.Instance.SetCoins(GameManager.Instance.GetCoins() + 30);
			}
            else if (lowerCaseEnemyName.Contains("monster"))
            {
				battleDialog.text += " Coin + 30";
				GameManager.Instance.SetCoins(GameManager.Instance.GetCoins() + 30);
			}
            // This can be replaced with a confirmation UI when we're ready
            yield return new WaitForSecondsRealtime(2f);

            var sceneChanger = GetComponent<SceneChangeInvokable>();
            if (lowerCaseEnemyName.Contains("horse"))
            {
                // if defeat the boss, go to ending scene
                sceneChanger.sceneName = "EndingStory";
            }
            else
            {
                sceneChanger.sceneName = GameManager.Instance.PrepareForReturnFromCombat();
            }
            sceneChanger.Invoke();
        }
        else
        {
             loseSound.Play();
            battleDialog.text = "You were vanquished!";
            //move back to checkpoint
            yield return new WaitForSecondsRealtime(3f);
            // Debug.Log("AFter being defeated " + GameManager.Instance.GetPlayerHealth());
            GameManager.Instance.LoadCheckpoint();
        }
    }

    IEnumerator DeathDialogues()
    {
        if (enemyReference.name.ToLower().Contains("skel"))
        {
            battleDialog.color = Color.red;
            battleDialog.text = "I will pick a bone with you next time!";
            yield return new WaitForSeconds(2.5f);
        }
        else if (enemyReference.name.ToLower().Contains("eye"))
        {
            battleDialog.color = Color.red;
            battleDialog.text = "I did not... see that coming...";
            yield return new WaitForSeconds(2.5f);
        }
        else if (enemyReference.name.ToLower().Contains("horse"))
        {
            battleDialog.color = Color.red;
            battleDialog.text = "This is just the beginning";
            yield return new WaitForSeconds(2f);
            battleDialog.color = Color.white;
            battleDialog.text = "The Boss disappeared into the ground";
            yield return new WaitForSeconds(2f);
        }
    }

    GameObject sendFireball(bool isFromPlayer = true)//todo: change for enemy
    {
        if (isFromPlayer)
        {
            GameObject fire = GameObject.Instantiate(fireboltAsset);
            fire.transform.position = player.transform.position + new Vector3(1, .5f, 1);
            fire.transform.rotation = new Quaternion(0, 0.70711f, 0, 0.70711f);
        }
        else
        {
            GameObject fire = GameObject.Instantiate(fireboltAsset);
            fire.transform.position = GameObject.FindWithTag("enemyReference").transform.position + new Vector3(-2, .5f, -1);
            fire.transform.rotation = new Quaternion(0, 0.70711f, 0, -0.70711f);
        }
        //var currentPrefabObject = GameObject.Instantiate(fireboltAsset);
        //int fireSrcOffset = isFromPlayer ? 1 : -1;
        //currentPrefabObject.transform.position = (isFromPlayer ? player : enemy).transform.position + new Vector3(fireSrcOffset, .5f, 0);
        //currentPrefabObject.transform.rotation = new Quaternion(0, 0.70711f * fireSrcOffset, 0, 0.70711f);

        return null;
    }
    
    private IEnumerator animateAndWaitThenDeactivate(string anim)
    {
        enemyAnimator.SetBool(anim, true);
        yield return new WaitForSeconds(1.5f);
        animator.Play("PlayerSlammed");
        slamSound.PlayDelayed(0.4f);
        yield return new WaitForSeconds(3.6f);
        enemyAnimator.SetBool(anim, false);
    }

    private IEnumerator ghostSlam(string anim)
    {
        ghostAnimator.SetBool(anim, true);
        slamSound.PlayDelayed(1f);
        yield return wait2sec;
        ghostAnimator.SetBool(anim, false);
    }

    GameObject sendSlam(bool isFromPlayer = true)
    {
        if(isFromPlayer)
        {
            StartCoroutine(ghostSlam("isCombat"));
        }
        else
        {
            if (enemyReference.name.ToLower().Contains("skel") || enemyReference.name.ToLower().Contains("eye") || enemyReference.name.ToLower().Contains("horse"))
            {
                StartCoroutine(animateAndWaitThenDeactivate("isCombat"));
            }
        }
        return null;
    }
    GameObject sendLightning(bool isFromPlayer = true)
    {
        var lightningObj = GameObject.Instantiate(lightningAsset);
        electrocuteSound.Play();
        var lightningComp = lightningObj.GetComponent<LightningBoltScript>();
        lightningComp.StartObject = GameObject.Find("ghost basic");
        lightningComp.EndObject = GameObject.FindWithTag("enemyReference");
        lightningComp.Generations = 3;

        return lightningObj;
    }

    private IEnumerator animateThrow(string anim)
    {
        enemyAnimator.SetBool(anim, true);
        knifeSound.PlayDelayed(1f);
        yield return wait2sec;
        enemyAnimator.SetBool(anim, false);
    }

    private IEnumerator bossThrow(string anim)
    {
        enemyAnimator.SetBool(anim, true);
        knifeSound.PlayDelayed(3.7f);
        yield return new WaitForSeconds(5f);
        enemyAnimator.SetBool(anim, false);
    }

    GameObject sendKnife(bool isFromPlayer = true)
    {
        if (isFromPlayer)
        {
            animator.Play("PlayerThrowKnife");
            knifeSound.PlayDelayed(0.05f);
        }
        else
        {
            if (enemyReference.name.ToLower().Contains("skel")) // sword throw skeleton
            {
                StartCoroutine(animateThrow("isThrow"));
            }
            else if (enemyReference.name.ToLower().Contains("horse"))
            {
                StartCoroutine(bossThrow("isThrow"));
            }
            else
            {
                animator.Play("EnemyThrowKnife");
                knifeSound.PlayDelayed(0.05f);
            }
        }
        return null;
    }

    GameObject selfHeal(bool isFromPlayer = true)
    {
        try
        {
            GameObject.Instantiate(healAsset, GameObject.Find("ghost basic").transform);
        } catch (Exception) { }
        playerHP.TakeDamage(-(int)CombatOptions.Heal);
        battleDialog.text = $"You gained {(int)CombatOptions.Heal}HP";
        healSound.Play();

        return null;
    }
    GameObject sendStun(bool isFromPlayer = true)
    {
        Destroy(stunObj);//remove prev stun effect if any

        animator.Play("PlayerStun");
        stunSound.PlayDelayed(0.2f);
        try
        {
            stunObj = Instantiate(enemyStunAsset, gameObject.transform);
        }
        catch (Exception) { }

        remaningStunTurns += 2;

        return null;
    }
    GameObject sendFireEle(bool isFromPlayer = true)
    {
        animator.Play("PlayerThrowFireEle");
        fireSound.Play();
        if (fireCount == 0)
		    fireCount++;
		return null;
	}

    GameObject sendEarthEle(bool isFromPlayer = true)
    {
		animator.Play("PlayerThrowEarthEle");
        earthSound.Play();
        if (earthCount == 0)
		    earthCount++;
		return null;
    }

    GameObject sendWaterEle(bool isFromPlayer = true)
    {
        waterSound.Play();
		animator.Play("PlayerThrowWaterEle");
		if (waterCount == 0)
		    waterCount++;
		return null;
    }

    GameObject sendElemental(bool isFromPlayer = true)
    {
        if (fireCount > 0)
        {
            animator.Play("PlayerThrowFireEle");
            fireSound.Play();
        }
		if (waterCount > 0)
		{
			animator.Play("PlayerThrowWaterEle");
            waterSound.Play();
        }
		if (earthCount > 0)
		{
			animator.Play("PlayerThrowEarthEle");
            earthSound.Play();
        }
		EleInfluenceDamange = (fireCount + earthCount + waterCount) * 10;
        if (fireCount > 0 && earthCount > 0 && waterCount > 0)
		{
			EleInfluenceDamange += 10;
		}
        return null;
	}

    private void UpdateDifficulty()
    {
        if (CheatCodeEntered())
        {
            playerPowerBoost = (playerPowerBoost + 1) % 7;
            Debug.Log($"Easier Difficulty, player's power boost = {playerPowerBoost}");
        }
        if (ResetDifficulty())
        {
            playerPowerBoost = 2;
            Debug.Log("Reset Difficulty");
        }
    }

    private static bool ResetDifficulty()
    {
        return Input.GetKeyDown(KeyCode.R);
    }

    private static bool CheatCodeEntered()
    {
        return Input.GetKeyDown(KeyCode.E);
    }


    /// <summary>
    /// combat buttons handlers
    /// </summary>
    public void OnSlamButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new (CombatOptions.Slam, 1.5f, sendSlam));
        }
    }

    public void OnFireButton()//todo: remove listener on mouse click
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new (CombatOptions.Fireball, 1f, sendFireball));
        }
    }

    public void OnDodgeButton()//todo: remove listener on mouse click, need this func?
    {
        if (state == BattleState.PLAYER_TURN)
        {
            playerDodged = true;
        }
    }

    public void OnElectrocuteButton() //for quick debug, not really needed
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.Electrocute, 2f, sendLightning));
        }
    }

    public void OnKnifeButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.Knife, 1f, sendKnife));
        }
    }
    public void OnStunButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.Stun, 1f, sendStun));
        }
    }
    public void OnHealButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.Heal, 0, selfHeal));
        }
    }

    public void OnFireEleButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.FireElement, 0, sendFireEle));
        }
    }
    public void OnEarthEleButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.EarthElement, 0, sendEarthEle));
        }
    }
    public void OnWaterEleButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.WaterElement, 0, sendWaterEle));
        }
    }
    public void OnElementalButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new(CombatOptions.ElementalInfluence, 0, sendElemental));
        }
    }

    public void OnEndTurnButton()//todo: remove listener on mouse click
    {
        if (state == BattleState.PLAYER_TURN)
        {
            StartCoroutine(ProcessTurn());
        }
    }
    
    public void RegisterPlayerTurnBeginListener(Func<string> fun)
    {
        playerTurnBeginListeners.Add(fun);
    }

    public void RegisterPlayerTurnEndListener(Func<string> fun)
    {
        playerTurnEndListeners.Add(fun);
    }

    public void RegisterStartBattleListener(Func<string> fun)
    {
        battleStartListeners.Add(fun);
    }
}
