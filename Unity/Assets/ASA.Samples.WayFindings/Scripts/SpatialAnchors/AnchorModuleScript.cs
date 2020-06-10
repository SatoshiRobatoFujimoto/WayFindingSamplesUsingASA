// Copyright (c) 2020 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Azure.SpatialAnchors;
using Microsoft.Azure.SpatialAnchors.Unity;
using UnityEngine;
using UnityEngine.XR.WSA;

public class AnchorModuleScript : MonoBehaviour, IAnchorModuleScript
{
    /// <summary>
    /// �r������p�I�u�W�F�N�g
    /// </summary>
    private static object lockObj = new object();

    /// <summary>
    /// Unity�̃��C���X���b�h��Ŏ��s�������������i�[����L���[
    /// </summary>
    private readonly Queue<Action> dispatchQueue = new Queue<Action>();

    /// <summary>
    /// Azure Spatial Anchors����擾����Anchor�̏����i�[����Dictionary
    /// </summary>
    private readonly Dictionary<string, CloudSpatialAnchor> locatedAnchors =
        new Dictionary<string, CloudSpatialAnchor>();

    /// <summary>
    /// Azure Spatial Anchors�̌������ɐݒ肷��<see cref="AnchorLocateCriteria"/>
    /// </summary>
    private AnchorLocateCriteria anchorLocateCriteria;

    /// <summary>
    /// Azure Spatial Anchors�̊Ǘ��N���X
    /// </summary>
    private SpatialAnchorManager cloudManager;

    /// <summary>
    /// Azure Spatial Anchors�������ɗ��p����Ď��N���X
    /// </summary>
    private CloudSpatialAnchorWatcher currentWatcher;

    /// <summary>
    /// Azure Spatial Anchors�̃p�����[�^�F�A���J�[���ӂ���������ۂ̒T���͈́i�P��:m�j
    /// </summary>
    private float distanceInMeters;

    /// <summary>
    /// Azure Spatial Anchors�̃p�����[�^�F�A���J�[���ӂ̌������Ɏ擾����A���J�[�̏����
    /// </summary>
    private int maxResultCount;

    /// <summary>
    /// Azure Spatial Anchors�̃p�����[�^�FSpatial Anchor�o�^���̃A���J�[�̎����i�P��:���j
    /// </summary>
    private int expiration;

    /// <summary>
    /// ����̃A���J�[�𒆐S�Ɍ��������ۂɌ��������A���J�[�ꗗ
    /// </summary>
    private List<string> findNearByAnchorIds = new List<string>();

    /// <summary>
    /// �A���J�[�擾��Ɏ��s����ʏ��������R���g���[���N���X
    /// </summary>
    public IASACallBackManager CallBackManager { get; set; }

#region Public Events

    /// <summary>
    /// �����󋵂��o�͂���C�x���g
    /// </summary>
    public event AnchorModuleProxy.FeedbackDescription OnFeedbackDescription;

#endregion

#region Internal Methods and Coroutines

    /// <summary>
    /// Unity�̃��C���X���b�h��Ŏ��s�������������L���[�ɓ������܂��B
    /// </summary>
    /// <param name="updateAction"></param>
    private void QueueOnUpdate(Action updateAction)
    {
        lock (dispatchQueue)
        {
            dispatchQueue.Enqueue(updateAction);
        }
    }

#endregion

#region Unity Lifecycle

    /// <summary>
    /// ���������������{���܂�
    /// </summary>
    public void Start()
    {
        try
        {
            // Azure Spatial Anchors�Ǘ��p�̃R���|�[�l���g���擾���܂��B
            cloudManager = GetComponent<SpatialAnchorManager>();

            // Azure Spatial Anchors�T�[�r�X���ďo�����Ƃ��ɔ�������C�x���g�����蓖�Ă܂��B
            // Azure Spatial Anchors����擾�����A���J�[�������ƂɃA���J�[�̐ݒu�����������ۂɔ�������C�x���g
            cloudManager.AnchorLocated += CloudManager_AnchorLocated;

            // Azure Spatial Anchors����擾�����A���J�[�ݒu���������ׂĊ�������ƌĂ΂��C�x���g
            cloudManager.LocateAnchorsCompleted += CloudManager_LocateAnchorsCompleted;

            // Azure Spatial Anchors�ւ̌���������ݒ肷��N���X�̃C���X�^���X��
            anchorLocateCriteria = new AnchorLocateCriteria();
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// �t���[�����Ɏ��s���鏈�������{���܂��B
    /// </summary>
    public void Update()
    {
        try
        {
            // Unity�̃��C���X���b�h��Ŏ��s�������������L���[������o���������J�n����B
            lock (dispatchQueue)
            {
                if (dispatchQueue.Count > 0)
                {
                    dispatchQueue.Dequeue()();
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// �I�u�W�F�N�g�̌㏈���i�p���j�����{���܂��B
    /// </summary>
    public void OnDestroy()
    {
        try
        {
            if (cloudManager != null && cloudManager.Session != null)
            {
                cloudManager.DestroySession();
            }

            if (currentWatcher != null)
            {
                currentWatcher.Stop();
                currentWatcher = null;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

#endregion

#region Public Methods

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X�Ƃ̐ڑ����s���A�Z�V�������J�n���܂��B
    /// </summary>
    /// <returns></returns>
    public async Task StartAzureSession()
    {
        try
        {
            Debug.Log("\nAnchorModuleScript.StartAzureSession()");

            OutputLog("Starting Azure session... please wait...");

            if (cloudManager.Session == null)
            {
                // Creates a new session if one does not exist
                await cloudManager.CreateSessionAsync();
            }

            // Starts the session if not already started
            await cloudManager.StartSessionAsync();

            OutputLog("Azure session started successfully");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X�Ƃ̐ڑ����~���܂��B
    /// </summary>
    /// <returns></returns>
    public async Task StopAzureSession()
    {
        try
        {
            Debug.Log("\nAnchorModuleScript.StopAzureSession()");

            OutputLog("Stopping Azure session... please wait...");

            // Stops any existing session
            cloudManager.StopSession();

            // Resets the current session if there is one, and waits for any active queries to be stopped
            await cloudManager.ResetSessionAsync();

            OutputLog("Azure session stopped successfully", isOverWrite: true);
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// Azure Spatial Anchors����擾�ς݂�Spatial Anchor��AppProperties���ꊇ�ŕύX���܂��B
    /// �L�[�����łɑ��݂���ꍇ��replace�p�����[�^�̒l�ɉ����Ēu�����A�ǋL��؂�ւ��ď��������{���܂��B
    /// </summary>
    /// <param name="key">AppProperties�̃L�[</param>
    /// <param name="val">�L�[�ɑΉ�����l</param>
    /// <param name="replace">true:�㏑���Afalse:�J���}��؂�ŒǋL</param>
    public async void UpdatePropertiesAll(string key, string val, bool replace = true)
    {
        try
        {
            OutputLog("Trying to update AppProperties of Azure anchors");
            foreach (var info in locatedAnchors.Values)
            {
                await UpdateProperties(info, key, val, replace);
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X�ɃA���J�[��ǉ����܂��B
    /// </summary>
    /// <param name="theObject">Spatial Anchor�̏��Ƃ��ēo�^���錻����Ԃɐݒu�����I�u�W�F�N�g</param>
    /// <param name="appProperties">Spatial Anchor�Ɋ܂߂���</param>
    /// <returns>�o�^����AnchorId</returns>
    public async Task<string> CreateAzureAnchor(GameObject theObject, IDictionary<string, string> appProperties)
    {
        try
        {
            Debug.Log("\nAnchorModuleScript.CreateAzureAnchor()");

            OutputLog("Creating Azure anchor");

            // Azure Spatial Anchors�T�[�r�X�̓o�^�ɕK�v��Native Anchor�̐ݒ���s���܂��B
            theObject.CreateNativeAnchor();

            OutputLog("Creating local anchor");

            // Azure Spatial Anchors�T�[�r�X�ɓo�^����Spatial Anchor�̏����������܂��B
            var localCloudAnchor = new CloudSpatialAnchor();

            // Spatial Anchor�ɂӂ��߂�����i�[���܂��B
            foreach (var key in appProperties.Keys)
            {
                localCloudAnchor.AppProperties.Add(key, appProperties[key]);
            }


            // Native Anchor�̃|�C���^��n���܂��B
            localCloudAnchor.LocalAnchor = theObject.FindNativeAnchor().GetPointer();

            // Native Anchor������ɐ�������Ă��邩���m�F���܂��B�����Ɏ��s���Ă��鎞�͏I�����܂��B
            if (localCloudAnchor.LocalAnchor == IntPtr.Zero)
            {
                OutputLog("Didn't get the local anchor...", LogType.Error);
                return null;
            }

            Debug.Log("Local anchor created");

            // Spatial Anchor�̎�����ݒ肵�܂��B���̓�����Azure Spatial Anchors�T�[�r�X���Anchor���c��܂��B
            localCloudAnchor.Expiration = DateTimeOffset.Now.AddDays(expiration);

            OutputLog("Move your device to capture more environment data: 0%");

            // Spatial Anchor�̓o�^�ɕK�v�ȋ�Ԃ̓����_���K�v�\���ɂȂ��Ă��邩���m�F���܂��B
            // RecommendedForCreateProgress��100%�ŕK�v�ȏ�񂪎��W�ł��Ă��܂��iHoloLens�̏ꍇ�قڈ�u�ł����܂��j
            do
            {
                await Task.Delay(330);
                var createProgress = cloudManager.SessionStatus.RecommendedForCreateProgress;
                QueueOnUpdate(() => OutputLog($"Move your device to capture more environment data: {createProgress:0%}",
                    isOverWrite: true));
            } while (!cloudManager.IsReadyForCreate);

            try
            {
                OutputLog("Creating Azure anchor... please wait...");

                // Azure Spatial Anchors�ɓo�^�����݂܂��B
                await cloudManager.CreateAnchorAsync(localCloudAnchor);

                // ����ɓo�^�ł����ꍇ�͓o�^���ʁiAnchorId�j���i�[���ꂽ�I�u�W�F�N�g���ԋp����܂��B
                var success = localCloudAnchor != null;
                if (success)
                {
                    OutputLog($"Azure anchor with ID '{localCloudAnchor.Identifier}' created successfully");
                    locatedAnchors.Add(localCloudAnchor.Identifier, localCloudAnchor);
                    return localCloudAnchor.Identifier;
                }

                OutputLog("Failed to save cloud anchor to Azure", LogType.Error);
            }
            catch (Exception ex)
            {
                Debug.Log(ex.ToString());
            }

            return null;
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// �w�肳�ꂽAnchorId�œo�^���ꂽAnchor�𒆐S�ɑ��̃A���J�[�����݂��邩���������{���܂��B
    /// </summary>
    /// <param name="anchorId">��ɂȂ�AnchorId</param>
    public void FindNearByAnchor(string anchorId)
    {
        try
        {
            anchorLocateCriteria.Identifiers = new string[0];
            Debug.Log("\nAnchorModuleScript.FindAzureAnchor()");
            OutputLog("Trying to find near by Azure anchor");

            // ���̃N���X�ŊǗ����Ă���擾�ς�Spatial Anchor�̈ꗗ�̒��Ɏw���Anchor�����݂��邩�m�F���܂��B
            if (!locatedAnchors.ContainsKey(anchorId))
            {
                OutputLog($"Not found anchor.id:{anchorId}.", LogType.Error);
                return;
            }

            // Azure Spatial Anchors���������������ݒ肵�܂��B
            // �A���J�[���ӂ��������邽�߂ɂ�Criteria��NearAnchorCriteria�̃C���X�^���X�����蓖�Ă܂��B
            anchorLocateCriteria.NearAnchor = new NearAnchorCriteria();

            // ��_�ɂȂ�Anchor�̏���ݒ肵�܂��B
            anchorLocateCriteria.NearAnchor.SourceAnchor = locatedAnchors[anchorId];

            // �T���͈͂ƁA�������o����ݒ肵�܂��B
            anchorLocateCriteria.NearAnchor.DistanceInMeters = distanceInMeters;
            anchorLocateCriteria.NearAnchor.MaxResultCount = maxResultCount;

            // �T�����̃��[����ݒ肵�܂��B���ӒT���ɂ�AnyStrategy��ݒ肵�܂��B
            anchorLocateCriteria.Strategy = LocateStrategy.AnyStrategy;

            Debug.Log(
                $"Anchor locate criteria configured to Search Near by Azure anchor ID '{anchorLocateCriteria.NearAnchor.SourceAnchor.Identifier}'");

            // �A���J�[�̒T�����J�n���܂��B���̏����͎��Ԃ������邽��Azure Spatial Anchors�ł�
            // Watcher�𐶐����ʃX���b�h��Ŕ񓯊����������{����܂��B
            // Anchor�̒T���Ɣz�u������������񂩂珇��AnchorLocated�C�x���g���������܂��B
            // �擾����Spatial Anchor�̐ݒu�����ׂĊ��������LocatedAnchorsComplete�C�x���g���������܂��B
            if (cloudManager != null && cloudManager.Session != null)
            {
                currentWatcher = cloudManager.Session.CreateWatcher(anchorLocateCriteria);
                Debug.Log("Watcher created");
                OutputLog("Looking for Azure anchor... please wait...");
            }
            else
            {
                OutputLog("Attempt to create watcher failed, no session exists", LogType.Error);
                currentWatcher = null;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// �w�肳�ꂽAnchorId�ɑΉ�����Spatial Anchor��Azure Spatial Anchors�T�[�r�X����擾���܂��B
    /// </summary>
    /// <param name="azureAnchorIds"></param>
    public void FindAzureAnchorById(params string[] azureAnchorIds)
    {
        try
        {
            Debug.Log("\nAnchorModuleScript.FindAzureAnchor()");

            OutputLog("Trying to find Azure anchor");

            var anchorsToFind = new List<string>();

            if (azureAnchorIds != null && azureAnchorIds.Length > 0)
            {
                anchorsToFind.AddRange(azureAnchorIds);
            }
            else
            {
                OutputLog("Current Azure anchor ID is empty", LogType.Error);
                return;
            }

            // Azure Spatial Anchors���������������ݒ肵�܂��B
            anchorLocateCriteria = new AnchorLocateCriteria();

            // ��������AnchorId�̃��X�g��ݒ肵�܂��B
            anchorLocateCriteria.Identifiers = anchorsToFind.ToArray();

            // ��x�擾�����A���J�[���ɑ΂��čĎ擾�����ꍇ�Ƀ��[�J���̏����L���b�V���Ƃ��ė��p���邩��ݒ肵�܂��B
            // ����̓L���b�V�����o�C�p�X���邽�߁A����Azure Spatial Anchors�֖₢���킹���������܂��B
            anchorLocateCriteria.BypassCache = true;

            // �A���J�[�̒T�����J�n���܂��B���̏����͎��Ԃ������邽��Azure Spatial Anchors�ł�
            // Watcher�𐶐����ʃX���b�h��Ŕ񓯊����������{����܂��B
            // Anchor�̒T���Ɣz�u������������񂩂珇��AnchorLocated�C�x���g���������܂��B
            // �擾����Spatial Anchor�̐ݒu�����ׂĊ��������LocatedAnchorsComplete�C�x���g���������܂��B
            if (cloudManager != null && cloudManager.Session != null)
            {
                currentWatcher = cloudManager.Session.CreateWatcher(anchorLocateCriteria);

                Debug.Log("Watcher created");
                OutputLog("Looking for Azure anchor... please wait...");
            }
            else
            {
                OutputLog("Attempt to create watcher failed, no session exists", LogType.Error);

                currentWatcher = null;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X����擾�ς݂̂��ׂẴA���J�[���폜���܂��B
    /// </summary>
    public async void DeleteAllAzureAnchor()
    {
        try
        {
            Debug.Log("\nAnchorModuleScript.DeleteAllAzureAnchor()");

            // Notify AnchorFeedbackScript
            OutputLog("Trying to find Azure anchor...");

            foreach (var AnchorInfo in locatedAnchors.Values)
            {
                // Delete the Azure anchor with the ID specified off the server and locally
                await cloudManager.DeleteAnchorAsync(AnchorInfo);
            }

            locatedAnchors.Clear();

            OutputLog("Trying to find Azure anchor...Successfully");
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// Anchor�������������s���邽�߂̃R���g���[����ݒ肵�܂��B
    /// </summary>
    /// <param name="iasaCallBackManager"></param>
    public void SetASACallBackManager(IASACallBackManager iasaCallBackManager)
    {
        CallBackManager = iasaCallBackManager;
    }

    /// <summary>
    /// Spatial Anchor�̌����͈͂�ݒ肵�܂��B
    /// </summary>
    /// <param name="distanceInMeters">�����͈́i�P��:m�j</param>
    public void SetDistanceInMeters(float distanceInMeters)
    {
        this.distanceInMeters = distanceInMeters;
    }

    /// <summary>
    /// Spatial Anchor�̎�����ݒ肵�܂�
    /// </summary>
    /// <param name="expiration">Anchor�̓o�^���ԁi�P��:���j</param>
    public void SetExpiration(int expiration)
    {
        this.expiration = expiration;
    }

    /// <summary>
    /// Spatial Anchor�̓�����������ݒ肵�܂��B
    /// </summary>
    /// <param name="distanceInMeters">������</param>
    public void SetMaxResultCount(int maxResultCount)
    {
        this.maxResultCount = maxResultCount;
    }
#endregion

#region Private Methods
    /// <summary>
    /// �w�肳�ꂽSpatial Anchor��AppProperties��ύX���܂��B
    /// �L�[�����łɑ��݂���ꍇ��replace�p�����[�^�̒l�ɉ����Ēu�����A�ǋL��؂�ւ��ď��������{���܂��B
    /// </summary>
    /// <param name="currentCloudAnchor">�ύX�Ώۂ�Spatial Anchor�̏��</param>
    /// <param name="key">AppProperties�̃L�[</param>
    /// <param name="val">�L�[�ɑΉ�����l</param>
    /// <param name="replace">true:�㏑���Afalse:�J���}��؂�ŒǋL</param>
    /// <returns></returns>
    private async Task UpdateProperties(CloudSpatialAnchor currentCloudAnchor, string key, string val,
        bool replace = true)
    {
        try
        {
            OutputLog($"anchor properties.id:{currentCloudAnchor.Identifier} -- key:{key},val:{val}....");
            if (currentCloudAnchor != null)
            {
                if (currentCloudAnchor.AppProperties.ContainsKey(key))
                {
                    if (replace || currentCloudAnchor.AppProperties[key].Length == 0)
                    {
                        currentCloudAnchor.AppProperties[key] = val;
                    }
                    else
                    {
                        currentCloudAnchor.AppProperties[key] = currentCloudAnchor.AppProperties[key] + "," + val;
                    }
                }
                else
                {
                    currentCloudAnchor.AppProperties.Add(key, val);
                }

                // Start watching for Anchors
                if (cloudManager != null && cloudManager.Session != null)
                {
                    await cloudManager.Session.UpdateAnchorPropertiesAsync(currentCloudAnchor);
                    var result = await cloudManager.Session.GetAnchorPropertiesAsync(currentCloudAnchor.Identifier);

                    OutputLog(
                        $"anchor properties.id:{currentCloudAnchor.Identifier} -- key:{key},val:{val}....successfully",
                        isOverWrite: true);
                }
                else
                {
                    OutputLog("Attempt to create watcher failed, no session exists", LogType.Error);
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// �����o�߂��o�͂��܂��B
    /// </summary>
    /// <param name="message">���b�Z�[�W</param>
    /// <param name="logType">�o�̓��O�̎��</param>
    /// <param name="isOverWrite">���O�̃��b�Z�[�W���㏑������</param>
    /// <param name="isReset">���b�Z�[�W���N���A����</param>
    private void OutputLog(string message, LogType logType = LogType.Log, bool isOverWrite = false,
        bool isReset = false)
    {
        try
        {
            OnFeedbackDescription?.Invoke(message, isOverWrite, isReset);
            switch (logType)
            {
                case LogType.Log:
                    Debug.Log(message);
                    break;
                case LogType.Error:
                    Debug.LogError(message);
                    break;
                case LogType.Warning:
                    Debug.LogError(message);
                    break;
                default:
                    Debug.Log(message);
                    break;
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

#endregion

#region Event Handlers

    /// <summary>
    /// Spatial Anchor�̐ݒu�����������ꍇ�ɔ�������C�x���g�Ŏ��s���鏈��
    /// </summary>
    /// <param name="sender">sender</param>
    /// <param name="args">args</param>
    private void CloudManager_AnchorLocated(object sender, AnchorLocatedEventArgs args)
    {
        try
        {
            // �ݒu�����A���J�[�̏�Ԃɉ����ď��������{���܂��B
            if (args.Status == LocateAnchorStatus.Located || args.Status == LocateAnchorStatus.AlreadyTracked)
            {
                //FindNearbyAnchors��Anchor�����������ꍇAppProperties����ɂȂ�܂��i�o�O�H�j
                //���̂��߁AFindNearbyAnchors�̌����Ō��������A���J�[��ID�̂ݏW�񂵂��ׂĂ̔z�u��������
                //FindAzureAnchorById�ōĎ擾��������B���̏�����CloudManager_LocateAnchorsCompleted���Ŏ��{���܂��B
                if (IsNearbyMode())
                {
                    var id = args.Anchor.Identifier;
                    QueueOnUpdate(() => Debug.Log($"Find near by Anchor id:{id}"));
                    lock (lockObj)
                    {
                        findNearByAnchorIds.Add(id);
                    }
                }
                else
                {
                    QueueOnUpdate(() => Debug.Log("Anchor recognized as a possible Azure anchor"));
                    // �擾����Spatial Anchor�̏������X�g�Ɋi�[���܂��B
                    lock (lockObj)
                    {
                        if (!locatedAnchors.ContainsKey(args.Anchor.Identifier))
                        {
                            locatedAnchors.Add(args.Anchor.Identifier, args.Anchor);
                        }
                    }

                    // �擾����Spatial Anchor�̏�񂩂�Unity�̃I�u�W�F�N�g�𐶐����A������Ԃ̐������ʒu�ɔz�u���܂��B
                    QueueOnUpdate( () =>
                    {
                        var currentCloudAnchor = args.Anchor;

                        Debug.Log("Azure anchor located successfully");

                        GameObject point = null;

                        // Spatial Anchor�ɑΉ�����Unity�I�u�W�F�N�g�𐶐����鏈�����ďo���܂��B
                        if(CallBackManager != null && !CallBackManager.OnLocatedAnchorObject(currentCloudAnchor.Identifier,
                            locatedAnchors[currentCloudAnchor.Identifier].AppProperties, out point))
                        {
                            return;
                        }

                        if (point == null)
                        {
                            OutputLog("Not Anchor Object", LogType.Error);
                            return;
                        }

                        point.SetActive(true);
                        
                        // Notify AnchorFeedbackScript
                        OutputLog("Azure anchor located");

#if UNITY_ANDROID || UNITY_IOS
                    Pose anchorPose = Pose.identity;
                    anchorPose = currentCloudAnchor.GetPose();
#endif

#if WINDOWS_UWP || UNITY_WSA
                        // Native Anchor�𐶐����܂��B
                        point.CreateNativeAnchor();

                        OutputLog("Creating local anchor");

                        // Unity�I�u�W�F�N�g��������Ԃ̐������ʒu�ɔz�u���܂��B
                        if (currentCloudAnchor != null)
                        {
                            Debug.Log("Local anchor position successfully set to Azure anchor position");

                            point.GetComponent<WorldAnchor>().SetNativeSpatialAnchorPtr(currentCloudAnchor.LocalAnchor);
                        }
#else
                    Debug.Log($"Setting object to anchor pose with position '{anchorPose.position}' and rotation '{anchorPose.rotation}'");
                    point.transform.position = anchorPose.position;
                    point.transform.rotation = anchorPose.rotation;

                    // Create a native anchor at the location of the object in question
                    point.CreateNativeAnchor();
#endif
                    });
                }
            }
            else
            {
                QueueOnUpdate(() =>
                    OutputLog(
                        $"Attempt to locate Anchor with ID '{args.Identifier}' failed, locate anchor status was not 'Located' but '{args.Status}'",
                        LogType.Error));
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// �����������ׂĂ�Spatial Anchor�̐ݒu��������������s���鏈���B
    /// </summary>
    /// <param name="sender">sender</param>
    /// <param name="args">args</param>
    private void CloudManager_LocateAnchorsCompleted(object sender, LocateAnchorsCompletedEventArgs args)
    {
        try
        {
            
            if (IsNearbyMode())
            {
                // NearbyAnchor�Ŏ擾�����ꍇ�AAppProperties�̏�񂪎擾�ł��Ȃ�����
                // ��x�z�u�ł���Spatial Anchor��AnchorId��ێ����Ă����ēxID�w��ŃA���J�[�̎擾�����{���܂��B
                QueueOnUpdate(() => OutputLog("Get the spatial anchors with Anchor App Properties."));
                QueueOnUpdate(() => FindAzureAnchorById(findNearByAnchorIds.ToArray()));
            }
            else
            {
                findNearByAnchorIds.Clear();
                QueueOnUpdate(() => OutputLog("Locate Azure anchors Complete."));

                if (!args.Cancelled)
                {
                    // �����������ׂĂ�Spatial Anchor�̐ݒu��������������s���܂��B
                    QueueOnUpdate(() => CallBackManager?.OnLocatedAnchorComplete());
                }
                else
                {
                    QueueOnUpdate(() => OutputLog("Attempt to locate Anchor Complete failed.", LogType.Error));
                }
            }
        }
        catch (Exception e)
        {
            Debug.Log(e);
            throw;
        }
    }

    /// <summary>
    /// NearbyAnchor�ł̌������ǂ������`�F�b�N���܂��B
    /// </summary>
    /// <returns>NearbyAnchor�ł̌�����true</returns>
    private bool IsNearbyMode()
    {
        return anchorLocateCriteria?.NearAnchor != null;
    }

#endregion
}