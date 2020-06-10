// Copyright (c) 2020 Takahiro Miyaura
// Released under the MIT license
// http://opensource.org/licenses/mit-license.php

using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;

/// <summary>
/// Azure Spatial Anchors�̏��������s���邽�߂̊Ǘ��N���X�Ɏ������C���^�[�t�F�[�X
/// </summary>
public interface IAnchorModuleScript
{
    /// <summary>
    /// ���������������{���܂�
    /// </summary>
    void Start();

    /// <summary>
    /// �t���[�����Ɏ��s���鏈�������{���܂��B
    /// </summary>
    void Update();

    /// <summary>
    /// �I�u�W�F�N�g�̌㏈���i�p���j�����{���܂��B
    /// </summary>
    void OnDestroy();

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X�Ƃ̐ڑ����s���A�Z�V�������J�n���܂��B
    /// </summary>
    /// <returns></returns>
    Task StartAzureSession();

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X�Ƃ̐ڑ����~���܂��B
    /// </summary>
    /// <returns></returns>
    Task StopAzureSession();

    /// <summary>
    /// Azure Spatial Anchors����擾�ς݂�Spatial Anchor��AppProperties���ꊇ�ŕύX���܂��B
    /// �L�[�����łɑ��݂���ꍇ��replace�p�����[�^�̒l�ɉ����Ēu�����A�ǋL��؂�ւ��ď��������{���܂��B
    /// </summary>
    /// <param name="key">AppProperties�̃L�[</param>
    /// <param name="val">�L�[�ɑΉ�����l</param>
    /// <param name="replace">true:�㏑���Afalse:�J���}��؂�ŒǋL</param>

    void UpdatePropertiesAll(string key, string val, bool replace = true);

    /// <summary>
    /// Spatial Anchor�̌����͈͂�ݒ肵�܂��B
    /// </summary>
    /// <param name="distanceInMeters">�����͈́i�P��:m�j</param>
    void SetDistanceInMeters(float distanceInMeters);

    /// <summary>
    /// Spatial Anchor�̓�����������ݒ肵�܂��B
    /// </summary>
    /// <param name="distanceInMeters">������</param>
    void SetMaxResultCount(int maxResultCount);

    /// <summary>
    /// Spatial Anchor�̎�����ݒ肵�܂�
    /// </summary>
    /// <param name="expiration">Anchor�̓o�^���ԁi�P��:���j</param>
    void SetExpiration(int expiration);

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X�ɃA���J�[��ǉ����܂��B
    /// </summary>
    /// <param name="theObject">Spatial Anchor�̏��Ƃ��ēo�^���錻����Ԃɐݒu�����I�u�W�F�N�g</param>
    /// <param name="appProperties">Spatial Anchor�Ɋ܂߂���</param>
    /// <returns>�o�^����AnchorId</returns>
    Task<string> CreateAzureAnchor(GameObject theObject, IDictionary<string, string> appProperties);
    
    /// <summary>
    /// �w�肳�ꂽAnchorId�œo�^���ꂽAnchor�𒆐S�ɑ��̃A���J�[�����݂��邩���������{���܂��B
    /// </summary>
    /// <param name="anchorId">��ɂȂ�AnchorId</param>
    void FindNearByAnchor(string anchorId);

    /// <summary>
    /// �w�肳�ꂽAnchorId�œo�^���ꂽAnchor�𒆐S�ɑ��̃A���J�[�����݂��邩���������{���܂��B
    /// </summary>
    /// <param name="anchorId">��ɂȂ�AnchorId</param>
    void FindAzureAnchorById(params string[] azureAnchorIds);

    /// <summary>
    /// Azure Spatial Anchors�T�[�r�X����擾�ς݂̂��ׂẴA���J�[���폜���܂��B
    /// </summary>
    void DeleteAllAzureAnchor();

    /// <summary>
    /// �����󋵂��o�͂���C�x���g
    /// </summary>
    event AnchorModuleProxy.FeedbackDescription OnFeedbackDescription;

    /// <summary>
    /// �A���J�[�擾��Ɏ��s����ʏ��������R���g���[���N���X
    /// </summary>
    IASACallBackManager CallBackManager { set; get; }

    /// <summary>
    /// Anchor�������������s���邽�߂̃R���g���[����ݒ肵�܂��B
    /// </summary>
    /// <param name="iasaCallBackManager"></param>
    void SetASACallBackManager(IASACallBackManager iasaCallBackManager);
}