export interface TankDto {
    tankName: string;
    subheader: string;
    vesselType: string;
    vesselName: string;
    imoNumber: string;
    tankType: string;
    status: boolean;
}

export interface SelectOption {
    text: string;
    value: string;
}

export interface ManageTankResponse {
    tanks: TankDto[];
    imoNumberOptions: SelectOption[];
    tankTypeOptions: SelectOption[];
    tankNameOptions: SelectOption[];
    tankStatusOptions: SelectOption[];
}
export interface TankDto {
    tankId?: number;
    tankName: string;
    subheader: string;
    vesselType: string;
    vesselName: string;
    imoNumber: string;
    tankType: string;
    status: boolean;
    isDeleted?: boolean;
    isDownload?: boolean;
    projectName?: string;
    projectId?: number;
}