using DigitalRuby.LightningBolt;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using static Rune;

[Serializable]
public enum BattleState { START, PLAYER_TURN, ENEMY_TURN, WON, LOST }

public enum CombatOptions
{
    Slam = 20,
    Firebolt = 25,
    Electrocute = 40,
    Knife = 5,
    Stun = 7,
    Heal = 10
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
    private readonly List<Func<string>> playerTurnBeginListeners = new();
    private readonly List<Func<string>> playerTurnEndListeners = new();

    bool playerDodged = false;

    Animator animator;
    readonly List<TurnActions> turnActions = new ();
    public ResourceHandler resourceHandler = new();
    public Spell[] spells = new Spell[5];
    void Start()
    {
        animator = GetComponent<Animator>();
        state = BattleState.START;
        player = GameObject.FindWithTag("Player");
        enemy = GameObject.FindWithTag("Enemy");
        playerHP = player.GetComponent<PlayerHealthBar>();
        // playerHP.TakeDamage(100 - GameManager.Instance.GetPlayerHealth());
        enemyHP = enemy.GetComponentInChildren<PlayerHealthBar>();
        battleDialog = GameObject.FindWithTag("BattleDialog").GetComponent<TextMeshProUGUI>();
        
        //freeze rotation/position
        playerRBConstraints = player.GetComponentInChildren<Rigidbody>().constraints;
        player.GetComponentInChildren<Rigidbody>().constraints = (RigidbodyConstraints)122;//freeze position xz, rotation
        enemy.GetComponentInChildren<Rigidbody>().constraints = (RigidbodyConstraints)122;

        SetupSpells();

        StartCoroutine(SetupBattle());
    }
    
    /** 
     *  Going to need to move all of the spell creation to the GameManager,
     *  or at least some of it. We need to know the spells the player has
     *  when we replace them.
     */
    public void SetupSpells()
    {
        var stunCost = new int[4];
        stunCost[(int)FIRE] = 1;
        stunCost[(int)EARTH] = 1;
        spells[0] = new Spell {
            name = "Stun",
            effect = () =>
            {
                OnStunButton();
                return "Pressed spell 1!";
            },
            cost = stunCost
        };
        
        var throwKnifeCost = new int[4];
        throwKnifeCost[(int)FIRE] = 2;
        throwKnifeCost[(int)AIR] = 1;
        spells[1] = new Spell {
            name = "Knife throwing",
            effect = () =>
            {
                OnKnifeButton();
                return "Pressed spell 2!";
            },
            cost = throwKnifeCost
        };

        var healCost = new int[4];
        healCost[(int)WATER] = 2;
        healCost[(int)AIR] = 1;
        spells[2] = new Spell {
            name = "Heal",
            effect = () =>
            {
                OnHealButton();
                return "Pressed spell 3!";
            },
            cost = healCost
        };
        
        var lightningCost = new int[4];
        lightningCost[(int)FIRE] = 3;
        spells[3] = new Spell {
            name = "Electrocute",
            effect = () =>
            {
                OnElectrocuteButton();
                return "Pressed spell 4!";
            },
            cost = lightningCost
        };
    }

    IEnumerator SetupBattle()
    {
        battleDialog.text = "Fighting the enemy!";

        yield return new WaitForSeconds(dialogWaitTime);

        if (isPlayerFirstTurn)
            PlayerTurn();
        else
            EnemyTurn();
    }

    void PlayerTurn()
    {
        battleDialog.text = "What will you do?";
        state = BattleState.PLAYER_TURN;
        foreach (var fun in playerTurnBeginListeners)
        {
            fun();
        }
    }

    IEnumerator ProcessTurn()
    {
        foreach (var fun in playerTurnEndListeners)
        {
            fun();
        }

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
                int enemyNewHP = enemyHP.TakeDamage((int)action.action, false);
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
        } else if (remaningStunTurns < 1)
        {
            state = BattleState.ENEMY_TURN;
                Destroy(stunObj);
            StartCoroutine(EnemyTurn());
        } else
        {
            remaningStunTurns--;
            PlayerTurn();
        }
    }

    IEnumerator EnemyTurn()
    {
        int randomInt = Time.renderedFrameCount % 100;
        CombatOptions enemyAction = CombatOptions.Knife;
        String dialogText = "The enemy <harm> you";

        if (playerDodged)
            animator.Play("PlayerDodge");
     
        switch (randomInt)
        {
            case < 25:
                enemyAction = CombatOptions.Slam;
                battleDialog.text = playerDodged ? "You dodged enemy's slam!" : dialogText.Replace("<harm>", "slammed");
                if (!playerDodged) sendSlam(false);
                yield return new WaitForSeconds(1f);
                break;
            //case < 50: //fire looks good from player, not so much from enemy - likely due to position
                //enemyAction = CombatOptions.Firebolt;
                //battleDialog.text = dialogText.Replace("<harm>", "threw a firebolt at");
                //sendFirebolt(false);
                //yield return new WaitForSeconds(1f);
                //break;
            case < 75:
                enemyAction = CombatOptions.Electrocute;
                battleDialog.text = dialogText.Replace("<harm>", "electrocutes");
                var lightning = sendLightning();
                yield return new WaitForSeconds(1f);
                Destroy(lightning);
                break;
            default:
                sendKnife(false);
                battleDialog.text = dialogText.Replace("<harm>", "threw a knife at");
                yield return new WaitForSeconds(1f);
                break;
        }
        if (!playerDodged || enemyAction is CombatOptions.Electrocute)
            playerHP.TakeDamage((int)enemyAction, true);//todo: use other damage values? maybe * 1.5?

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
        if (state == BattleState.WON)
        {
            battleDialog.text = "You have prevailed!";
            // This can be replaced with a confirmation UI when we're ready
            yield return new WaitForSecondsRealtime(2f);
            var sceneChanger = GetComponent<SceneChangeInvokable>();
            sceneChanger.sceneName = GameManager.Instance.PrepareForReturnFromCombat();
            sceneChanger.Invoke();
        }
        else
        {
            battleDialog.text = "You were vanquished!";
            //move back to checkpoint
            yield return new WaitForSecondsRealtime(3f);
            // Debug.Log("AFter being defeated " + GameManager.Instance.GetPlayerHealth());
            GameManager.Instance.LoadCheckpoint();
        }
    }

    GameObject sendFirebolt(bool isFromPlayer = true)//todo: change for enemy
    {
        var currentPrefabObject = GameObject.Instantiate(fireboltAsset);
        int fireSrcOffset = isFromPlayer ? 1 : -1;
        currentPrefabObject.transform.position = (isFromPlayer ? player : enemy).transform.position + new Vector3(fireSrcOffset, .5f, 0);
        currentPrefabObject.transform.rotation = new Quaternion(0, 0.70711f * fireSrcOffset, 0, 0.70711f);

        return null;
    }
    GameObject sendSlam(bool isFromPlayer = true)
    {
        animator.Play((isFromPlayer ? "Enemy" : "Player") + "Slammed");

        return null;
    }
    GameObject sendLightning(bool isFromPlayer = true)
    {
        var lightningObj = GameObject.Instantiate(lightningAsset);
        var lightningComp = lightningObj.GetComponent<LightningBoltScript>();
        lightningComp.StartObject = player;
        lightningComp.EndObject = enemy;
        lightningComp.Generations = 3;

        return lightningObj;
    }

    GameObject sendKnife(bool isFromPlayer = true)
    { 
        animator.Play((isFromPlayer ? "Player" : "Enemy") + "ThrowKnife");

        return null;
    }

    GameObject selfHeal(bool isFromPlayer = true)
    {
        try
        {
            GameObject.Instantiate(healAsset, player.transform);
        } catch (Exception ignored) { }
        playerHP.TakeDamage(-(int)CombatOptions.Heal);
        battleDialog.text = $"You gained {(int)CombatOptions.Heal}HP";

        return null;
    }
    GameObject sendStun(bool isFromPlayer = true)
    {
        Destroy(stunObj);//remove prev stun effect if any

        animator.Play("PlayerStun");
        try
        {
            stunObj = Instantiate(enemyStunAsset, enemy.transform);
        }
        catch (Exception ignored) { }

        remaningStunTurns++;

        return null;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.K))
        {
            animator.Play("PlayerStun");
            try
            {
                //healObj = GameObject.Instantiate(healAsset, player.transform);
                healObj = GameObject.Instantiate(healAsset, player.transform);
            } catch (Exception ignored) { }
        }
        if (Input.GetKeyDown(KeyCode.Q))
        {
            //Destroy(healObj);
            Destroy(healObj);
        }
    }


    /// <summary>
    /// combat buttons handlers
    /// </summary>
    public void OnSlamButton()
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new (CombatOptions.Slam, 2f, sendSlam)) ;
        }
    }

    public void OnFireButton()//todo: remove listener on mouse click
    {
        if (state == BattleState.PLAYER_TURN)
        {
            turnActions.Add(new (CombatOptions.Firebolt, 2f, sendFirebolt));
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
}
